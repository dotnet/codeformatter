using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public class AnalysisEngine
    {
        private IList<Project> m_projects;

        private HashSet<string> m_ignoredSymbols;

        public static async Task<AnalysisEngine> Create(IEnumerable<string> projectPaths, IEnumerable<string> enabledSymbols = null, IEnumerable<string> ignoredSymbols = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (projectPaths == null || !projectPaths.Any())
            {
                throw new ArgumentException("Must specify at least one project", "projectPaths");
            }

            var projects = await Task.WhenAll(from path in projectPaths select MSBuildWorkspace.Create().OpenProjectAsync(path, cancellationToken));

            return new AnalysisEngine(projects, enabledSymbols, ignoredSymbols);
        }

        internal AnalysisEngine(IList<Project> projects, IEnumerable<string> enabledSymbols = null, IEnumerable<string> ignoredSymbols = null)
        {
            Debug.Assert(projects != null);
            m_projects = projects;

            m_ignoredSymbols = new HashSet<string>();
            if (ignoredSymbols != null)
            {
                foreach (var symbol in ignoredSymbols)
                {
                    if (!m_ignoredSymbols.Contains(symbol))
                    {
                        m_ignoredSymbols.Add(symbol);
                    }
                }
            }
        }

        public async Task PrintConditionalRegionInfoAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var regionInfo = await GetConditionalRegionInfo(cancellationToken);
            PrintConditionalRegionInfo(regionInfo);
        }

        private void PrintConditionalRegionInfo(IEnumerable<DocumentConditionalRegionInfo> regionInfo)
        {
            var originalForegroundColor = Console.ForegroundColor;

            int disabledCount = 0;
            int enabledCount = 0;
            int varyingCount = 0;
            int explicitlyVaryingCount = 0;

            foreach (var info in regionInfo)
            {
                foreach (var chain in info.Chains)
                {
                    foreach (var region in chain.Regions)
                    {
                        switch (region.State)
                        {
                            case ConditionalRegionState.AlwaysDisabled:
                                disabledCount++;
                                Console.ForegroundColor = ConsoleColor.Blue;
                                break;
                            case ConditionalRegionState.AlwaysEnabled:
                                enabledCount++;
                                Console.ForegroundColor = ConsoleColor.Green;
                                break;
                            case ConditionalRegionState.Varying:
                                varyingCount++;
                                if (region.ExplicitlyVaries)
                                {
                                    explicitlyVaryingCount++;
                                }
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                break;
                        }
                        Console.WriteLine(region);
                    }
                }
            }

            Console.ForegroundColor = originalForegroundColor;

            // Print summary
            Console.WriteLine();

            int totalRegionCount = disabledCount + enabledCount + varyingCount;
            if (totalRegionCount == 0)
            {
                Console.WriteLine("Did not find any conditional regions.");
            }

            Console.WriteLine("Found");
            Console.WriteLine("  {0,5} conditional regions total", totalRegionCount);

            string alwaysString = m_projects.Count > 1 ? "always " : string.Empty;

            if (disabledCount > 0)
            {
                Console.WriteLine("  {0,5} {1}disabled", disabledCount, alwaysString);
            }

            if (enabledCount > 0)
            {
                Console.WriteLine("  {0,5} {1}enabled", enabledCount, alwaysString);
            }

            if (varyingCount > 0)
            {
                Console.WriteLine("  {0,5} varying", varyingCount);
                Console.WriteLine("    {0,5} due to real varying symbols", varyingCount - explicitlyVaryingCount);
                Console.WriteLine("    {0,5} due to ignored symbols", explicitlyVaryingCount);
            }

            // TODO: Lines of dead code.  A chain struct might be useful because there are many operations on a chain.
            // This involves calculating unnecessary regions, converting those to line spans
        }

        public async Task RemoveUnnecessaryConditionalRegions(CancellationToken cancellationToken = default(CancellationToken))
        {
            var regionInfo = await GetConditionalRegionInfo(cancellationToken);
            await CleanUp.RemoveUnnecessaryRegions(m_projects[0].Solution.Workspace, regionInfo, cancellationToken);
        }

        /// <summary>
        /// Returns the intersection of <see cref="DocumentConditionalRegionInfo"/> in the given projects.
        /// The contained <see cref="Document"/> objects will all be from the first project.
        /// </summary>
        public async Task<IList<DocumentConditionalRegionInfo>> GetConditionalRegionInfo(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (m_projects.Count == 1)
            {
                return await GetConditionalRegionInfo(m_projects[0], d => true, cancellationToken);
            }

            // Intersect the set of files in the projects so that we only analyze the set of files shared between all projects
            var filePaths = m_projects[0].Documents.Select(d => d.FilePath);

            for (int i = 1; i < m_projects.Count; i++)
            {
                filePaths = filePaths.Intersect(
                    m_projects[i].Documents.Select(d => d.FilePath),
                    StringComparer.InvariantCultureIgnoreCase);
            }

            var filePathSet = new HashSet<string>(filePaths);
            Predicate<Document> shouldAnalyzeDocument = doc => filePathSet.Contains(doc.FilePath);

            // Intersect the conditional regions of each document shared between all the projects
            IList<DocumentConditionalRegionInfo> infoA = await GetConditionalRegionInfo(m_projects[0], shouldAnalyzeDocument, cancellationToken);

            for (int i = 1; i < m_projects.Count; i++)
            {
                var infoB = await GetConditionalRegionInfo(m_projects[i], shouldAnalyzeDocument, cancellationToken);
                infoA = IntersectConditionalRegionInfo(infoA, infoB);
            }

            return infoA;
        }

        /// <summary>
        /// Returns a sorted array of <see cref="DocumentConditionalRegionInfo"/> for the specified project, filtered by the given predicate.
        /// </summary>
        private async Task<DocumentConditionalRegionInfo[]> GetConditionalRegionInfo(Project project, Predicate<Document> predicate, CancellationToken cancellationToken)
        {
            var documentInfos = await Task.WhenAll(
                from document in project.Documents
                where predicate(document)
                select GetConditionalRegionInfo(document, cancellationToken));

            Array.Sort(documentInfos);

            return documentInfos;
        }

        private async Task<DocumentConditionalRegionInfo> GetConditionalRegionInfo(Document document, CancellationToken cancellationToken)
        {
            var chains = new List<ConditionalRegionChain>();
            var regions = new List<ConditionalRegion>();

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken) as CSharpSyntaxTree;
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            if (root.ContainsDirectives)
            {
                var currentDirective = root.GetFirstDirective(IsBranchingDirective);
                var visitedDirectives = new HashSet<DirectiveTriviaSyntax>();

                while (currentDirective != null)
                {
                    var chain = ParseConditionalRegionChain(currentDirective.GetLinkedDirectives(), visitedDirectives);
                    if (chain != null)
                    {
                        chains.Add(new ConditionalRegionChain(chain));
                    }

                    do
                    {
                        currentDirective = currentDirective.GetNextDirective(IsBranchingDirective);
                    } while (visitedDirectives.Contains(currentDirective));
                }
            }

            return new DocumentConditionalRegionInfo(document, chains);
        }

        private List<ConditionalRegion> ParseConditionalRegionChain(List<DirectiveTriviaSyntax> directives, HashSet<DirectiveTriviaSyntax> visitedDirectives)
        {
            DirectiveTriviaSyntax previousDirective = null;
            var chain = new List<ConditionalRegion>();
            bool explicitlyVaries = false;

            for (int i = 0; i < directives.Count; i++)
            {
                var directive = directives[i];

                if (visitedDirectives.Contains(directive))
                {
                    // We've already visited this chain of linked directives
                    return null;
                }

                if (previousDirective != null)
                {
                    // Ignore chains with inactive directives because their conditions are not evaluated by the parser.
                    if (!previousDirective.IsActive)
                    {
                        return null;
                    }

                    // If a condition has been specified as explicitly varying, then all following conditions
                    // are implicitly varying because each successive directive depends on the condition of
                    // the preceding directive.
                    if (!explicitlyVaries && DependsOnIgnoredSymbols(previousDirective))
                    {
                        explicitlyVaries = true;
                    }

                    var region = new ConditionalRegion(previousDirective, directive, chain, chain.Count, explicitlyVaries);
                    chain.Add(region);
                }

                previousDirective = directive;
                visitedDirectives.Add(directive);
            }

            return chain;
        }

        private bool DependsOnIgnoredSymbols(DirectiveTriviaSyntax directive)
        {
            ExpressionSyntax condition = null;

            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                    condition = ((IfDirectiveTriviaSyntax)directive).Condition;
                    break;
                case SyntaxKind.ElifDirectiveTrivia:
                    condition = ((ElifDirectiveTriviaSyntax)directive).Condition;
                    break;
                case SyntaxKind.ElseDirectiveTrivia:
                case SyntaxKind.EndIfDirectiveTrivia:
                    // #endif directives don't have expressions, so they can't depend on ignored symbols.
                    // If an #else directive depends on an ignored symbol, we will have caught that earlier
                    // when looking at the corresponding #if directive.
                    return false;
                default:
                    Debug.Assert(false);
                    return false;
            }

            foreach (var child in condition.DescendantNodesAndSelf())
            {
                var identifier = child as IdentifierNameSyntax;
                if (identifier != null)
                {
                    if (m_ignoredSymbols.Contains(identifier.Identifier.ValueText))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsBranchingDirective(DirectiveTriviaSyntax directive)
        {
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Intersects two arrays of <see cref="DocumentConditionalRegionInfo"/> for the same document.
        /// The data contained in <param name="x"/> will be modified.
        /// Note that both <param name="x"/> and <param name="y"/> are assumed to be sorted.
        /// </summary>
        private static IList<DocumentConditionalRegionInfo> IntersectConditionalRegionInfo(IList<DocumentConditionalRegionInfo> x, IList<DocumentConditionalRegionInfo> y)
        {
            var info = new List<DocumentConditionalRegionInfo>();
            int i = 0;
            int j = 0;

            while (i < x.Count && j < y.Count)
            {
                var result = x[i].CompareTo(y[j]);

                if (result == 0)
                {
                    x[i].Intersect(y[j]);
                    info.Add(x[i]);

                    i++;
                    j++;
                }
                else if (result < 0)
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            return info;
        }
    }
}
