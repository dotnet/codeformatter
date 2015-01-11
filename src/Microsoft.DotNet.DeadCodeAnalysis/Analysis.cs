using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public static class Analysis
    {
        /// <summary>
        /// Returns the intersection of <see cref="DocumentConditionalRegionInfo"/> in the given projects.
        /// The contained <see cref="Document"/> objects will all be from the first project.
        /// </summary>
        public static async Task<DocumentConditionalRegionInfo[]> GetIntersectedConditionalRegionInfo(Project[] projects)
        {
            if (projects == null || projects.Length == 0)
            {
                throw new ArgumentException("Must specify at least one project", "projects");
            }

            if (projects.Length == 1)
            {
                return await GetConditionalRegionInfo(projects[0], d => true);
            }

            // Intersect the set of files in the projects so that we only analyze the set of files shared between all projects
            var filePaths = projects[0].Documents.Select(d => d.FilePath);

            for (int i = 1; i < projects.Length; i++)
            {
                filePaths = filePaths.Intersect(
                    projects[i].Documents.Select(d => d.FilePath),
                    StringComparer.InvariantCultureIgnoreCase);
            }

            var filePathSet = new HashSet<string>(filePaths);
            Predicate<Document> shouldAnalyzeDocument = doc => filePathSet.Contains(doc.FilePath);

            // Intersect the conditional regions of each document shared between all the projects
            var infoA = await GetConditionalRegionInfo(projects[0], shouldAnalyzeDocument);

            for (int i = 1; i < projects.Length; i++)
            {
                var infoB = await GetConditionalRegionInfo(projects[i], shouldAnalyzeDocument);
                IntersectConditionalRegionInfo(infoA, infoB);
            }

            return infoA;
        }

        /// <summary>
        /// Returns a sorted array of <see cref="DocumentConditionalRegionInfo"/> for the specified project, filtered by the given predicate.
        /// </summary>
        internal static async Task<DocumentConditionalRegionInfo[]> GetConditionalRegionInfo(Project project, Predicate<Document> predicate)
        {
            var documentInfos = await Task.WhenAll(
                from document in project.Documents
                where predicate(document)
                select GetConditionalRegionInfo(document));

            Array.Sort(documentInfos);

            return documentInfos;
        }

        /// <summary>
        /// Returns a list of condition region chains in a given document in prefix document order
        /// </summary>
        private static async Task<DocumentConditionalRegionInfo> GetConditionalRegionInfo(Document document)
        {
            var chains = new List<ConditionalRegionChain>();

            var syntaxTree = await document.GetSyntaxTreeAsync() as CSharpSyntaxTree;
            var root = syntaxTree.GetRoot();

            if (root.ContainsDirectives)
            {
                var currentDirective = root.GetFirstDirective(IsConditionalDirective);
                var visitedDirectives = new HashSet<DirectiveTriviaSyntax>();

                while (currentDirective != null)
                {
                    var directives = currentDirective.GetLinkedDirectives();

                    var chain = ParseConditionalRegionChain(directives, visitedDirectives);
                    if (!chain.IsDefault)
                    {
                        chains.Add(chain);
                    }

                    do
                    {
                        currentDirective = currentDirective.GetNextDirective(IsConditionalDirective);
                    } while (visitedDirectives.Contains(currentDirective));
                }
            }

            return new DocumentConditionalRegionInfo(document, chains);
        }

        private static ConditionalRegionChain ParseConditionalRegionChain(List<DirectiveTriviaSyntax> directives, HashSet<DirectiveTriviaSyntax> visitedDirectives)
        {
            DirectiveTriviaSyntax previousDirective = null;
            List<ConditionalRegion> chain = null;

            foreach (var directive in directives)
            {
                if (visitedDirectives.Contains(directive))
                {
                    // We've already visited this chain of linked directives
                    return default(ConditionalRegionChain);
                }

                if (previousDirective != null)
                {
                    // TODO: Check for ignored symbols. If there are any, then set the whole chain to be ignored.
                    var region = new ConditionalRegion(previousDirective, directive);

                    if (chain == null)
                    {
                        // TODO: If directives.Count == 2, which is a common case, this can be a singleton list
                        chain = new List<ConditionalRegion>();
                    }

                    chain.Add(region);
                }

                previousDirective = directive;
                visitedDirectives.Add(directive);
            }

            Debug.Assert(chain != null, "chain should never be null given a valid set of linked directives");
            return new ConditionalRegionChain(chain);
        }

        private static string GetEffectiveExpression(DirectiveTriviaSyntax directive)
        {
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                    return ((IfDirectiveTriviaSyntax)directive).Condition.ToString();

                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                    {
                        var directives = directive.GetRelatedDirectives();

                        string expression = string.Empty;

                        if (directives.Count > 2)
                        {
                            expression = string.Format("!({0})", ((IfDirectiveTriviaSyntax)directives[0]).Condition.ToString());
                        }

                        for (int i = 1; i < directives.Count; i++)
                        {
                            var currentDirective = directives[i];
                            if (currentDirective.GetLocation() == directive.GetLocation())
                            {
                                if (directive.CSharpKind() == SyntaxKind.ElifDirectiveTrivia)
                                {
                                    expression = string.Format("{0} && ({1})", expression, ((ElifDirectiveTriviaSyntax)currentDirective).Condition.ToString());
                                }
                                break;
                            }

                            expression = string.Format("{0} && !({1})", expression, ((ElifDirectiveTriviaSyntax)currentDirective).Condition.ToString());
                        }

                        return expression;
                    }
            }

            return string.Empty;
        }

        private static bool IsConditionalDirective(DirectiveTriviaSyntax directive)
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

        private static bool IsBranchTaken(DirectiveTriviaSyntax directive)
        {
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                    return ((IfDirectiveTriviaSyntax)directive).BranchTaken;
                case SyntaxKind.ElifDirectiveTrivia:
                    return ((ElifDirectiveTriviaSyntax)directive).BranchTaken;
                case SyntaxKind.ElseDirectiveTrivia:
                    return ((ElseDirectiveTriviaSyntax)directive).BranchTaken;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Intersects two arrays of <see cref="DocumentConditionalRegionInfo"/> for the same document.
        /// The data contained in <param name="x"/> will be modified.
        /// Note that both <param name="x"/> and <param name="y"/> are assumed to be sorted.
        /// </summary>
        private static void IntersectConditionalRegionInfo(DocumentConditionalRegionInfo[] x, DocumentConditionalRegionInfo[] y)
        {
            int i = 0;
            int j = 0;

            while (i < x.Length && j < y.Length)
            {
                var result = x[i].CompareTo(y[j]);

                if (result == 0)
                {
                    x[i].Intersect(y[j]);

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
        }
    }
}
