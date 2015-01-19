using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public partial class AnalysisEngine
    {
        public class Options
        {
            public IEnumerable<Document> Documents { get; private set; }

            public IReadOnlyDictionary<string, Tristate> SymbolStates { get; private set; }

            internal Options(
                IEnumerable<Project> projects = null,
                IEnumerable<string> projectPaths = null,
                IEnumerable<string> sourcePaths = null,
                IEnumerable<IEnumerable<string>> symbolConfigurations = null,
                IEnumerable<string> alwaysIgnoredSymbols = null,
                IEnumerable<string> alwaysDefinedSymbols = null,
                IEnumerable<string> alwaysDisabledSymbols = null)
            {
                if (projectPaths != null)
                {
                    projects = Task.WhenAll(from path in projectPaths select MSBuildWorkspace.Create().OpenProjectAsync(path, CancellationToken.None)).Result;
                }
                if (projects != null)
                {
                    Documents = GetSharedDocuments(projects);
                }

                if (sourcePaths != null)
                {
                    var projectId = ProjectId.CreateNewId("AnalysisProject");
                    var solution = new CustomWorkspace()
                        .CurrentSolution
                        .AddProject(projectId, "AnalysisProject", "AnalysisProject", LanguageNames.CSharp);

                    foreach (var path in sourcePaths)
                    {
                        var documentId = DocumentId.CreateNewId(projectId);
                        solution = solution.AddDocument(
                            documentId,
                            Path.GetFileName(path),
                            new FileTextLoader(path, Encoding.UTF8));
                    }

                    Documents = solution.Projects.Single().Documents;
                }

                SymbolStates = CalculateSymbolStates(
                    alwaysDisabledSymbols,
                    alwaysDefinedSymbols,
                    alwaysIgnoredSymbols,
                    symbolConfigurations);
            }

            private static IEnumerable<Document> GetSharedDocuments(IEnumerable<Project> projects)
            {
                var it = projects.GetEnumerator();
                if (!it.MoveNext())
                {
                    return Enumerable.Empty<Document>();
                }

                var filePaths = it.Current.Documents.Select(d => d.FilePath);

                while (it.MoveNext())
                {
                    filePaths = filePaths.Intersect(
                        it.Current.Documents.Select(d => d.FilePath),
                        StringComparer.InvariantCultureIgnoreCase);
                }

                var filePathSet = new HashSet<string>(filePaths);
                return projects.First().Documents.Where(d => filePathSet.Contains(d.FilePath));
            }

            internal static Dictionary<string, Tristate> CalculateSymbolStates(
                IEnumerable<string> alwaysDisabledSymbols,
                IEnumerable<string> alwaysDefinedSymbols,
                IEnumerable<string> alwaysIgnoredSymbols,
                IEnumerable<IEnumerable<string>> symbolConfigurations)
            {
                var symbolStates = new Dictionary<string, Tristate>();

                AddExplicitSymbolStates(symbolStates, alwaysDisabledSymbols, Tristate.False);
                AddExplicitSymbolStates(symbolStates, alwaysDefinedSymbols, Tristate.True);
                AddExplicitSymbolStates(symbolStates, alwaysIgnoredSymbols, Tristate.Varying);

                if (symbolConfigurations == null || !symbolConfigurations.Any())
                {
                    return symbolStates;
                }

                // The symbols which are defined in all configurations and do not have an explicit value are always enabled
                var configurations = symbolConfigurations.ToArray();
                var enabledSymbols = configurations[0];

                for (int i = 1; i < configurations.Length; i++)
                {
                    enabledSymbols = enabledSymbols.Intersect(configurations[i]);
                }

                foreach (var symbol in enabledSymbols)
                {
                    if (!symbolStates.ContainsKey(symbol))
                    {
                        symbolStates[symbol] = Tristate.True;
                    }
                }

                // The symbols which only appear in some configurations and do not have an explicit value are varying
                foreach (var configuration in symbolConfigurations)
                {
                    foreach (var symbol in configuration)
                    {
                        if (!symbolStates.ContainsKey(symbol))
                        {
                            symbolStates[symbol] = Tristate.Varying;
                        }
                    }
                }

                return symbolStates;
            }

            private static void AddExplicitSymbolStates(Dictionary<string, Tristate> symbolStates, IEnumerable<string> symbols, Tristate explicitState)
            {
                if (symbols == null)
                {
                    return;
                }

                foreach (var symbol in symbols)
                {
                    Tristate state;
                    if (symbolStates.TryGetValue(symbol, out state))
                    {
                        if (state == explicitState)
                        {
                            throw new ArgumentException(
                                string.Format("Symbol '{0}' appears in the {1} list multiple times",
                                    symbol, GetStateString(explicitState)));
                        }
                        else
                        {
                            throw new ArgumentException(
                                string.Format("Symbol '{0}' cannot be both {1} and {2}",
                                    symbol, GetStateString(state), GetStateString(explicitState)));
                        }
                    }
                    else
                    {
                        symbolStates[symbol] = explicitState;
                    }
                }
            }

            private static string GetStateString(Tristate state)
            {
                if (state == Tristate.False)
                {
                    return "always disabled";
                }
                else if (state == Tristate.True)
                {
                    return "always enabled";
                }
                else
                {
                    return "ignore";
                }
            }
        }
    }
}
