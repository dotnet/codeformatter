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

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public partial class AnalysisEngine
    {
        public class Options
        {
            private IEnumerable<IReadOnlyDictionary<string, Tristate>> m_symbolConfigurations;
            private Tristate m_undefinedSymbolValue;

            public IEnumerable<Document> Documents { get; private set; }

            internal Options(
                IEnumerable<Project> projects = null,
                IEnumerable<string> projectPaths = null,
                IEnumerable<string> sourcePaths = null,
                IEnumerable<IEnumerable<string>> symbolConfigurations = null,
                IEnumerable<string> alwaysIgnoredSymbols = null,
                IEnumerable<string> alwaysDefinedSymbols = null,
                IEnumerable<string> alwaysDisabledSymbols = null,
                Tristate undefinedSymbolValue = default(Tristate))
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

                m_symbolConfigurations = CalculateSymbolConfigurations(
                    alwaysDisabledSymbols,
                    alwaysDefinedSymbols,
                    alwaysIgnoredSymbols,
                    symbolConfigurations);

                m_undefinedSymbolValue = undefinedSymbolValue;
            }

            internal IEnumerable<PreprocessorExpressionEvaluator> GetPreprocessorExpressionEvaluators()
            {
                return m_symbolConfigurations.Select(config => new PreprocessorExpressionEvaluator(config, m_undefinedSymbolValue));
            }

            internal PreprocessorSymbolTracker GetPreprocessorSymbolTracker()
            {
                var specifiedSymbols = new HashSet<string>();

                foreach (var config in m_symbolConfigurations)
                {
                    foreach (string symbol in config.Keys)
                    {
                        specifiedSymbols.Add(symbol);
                    }
                }

                return new PreprocessorSymbolTracker(specifiedSymbols);
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

            private static IEnumerable<IReadOnlyDictionary<string, Tristate>> CalculateSymbolConfigurations(
                IEnumerable<string> alwaysDisabledSymbols,
                IEnumerable<string> alwaysDefinedSymbols,
                IEnumerable<string> alwaysIgnoredSymbols,
                IEnumerable<IEnumerable<string>> symbolConfigurations)
            {
                var explicitStates = new Dictionary<string, Tristate>();

                AddExplicitSymbolStates(explicitStates, alwaysDisabledSymbols, Tristate.False);
                AddExplicitSymbolStates(explicitStates, alwaysDefinedSymbols, Tristate.True);
                AddExplicitSymbolStates(explicitStates, alwaysIgnoredSymbols, Tristate.Varying);

                if (symbolConfigurations == null || !symbolConfigurations.Any())
                {
                    return new[] { explicitStates };
                }

                var configurationStateMaps = new List<Dictionary<string, Tristate>>();
                foreach (var configuration in symbolConfigurations)
                {
                    var stateMap = new Dictionary<string, Tristate>(explicitStates);

                    foreach (var symbol in configuration)
                    {
                        if (!stateMap.ContainsKey(symbol))
                        {
                            stateMap.Add(symbol, Tristate.True);
                        }
                    }

                    configurationStateMaps.Add(stateMap);
                }

                return configurationStateMaps;   
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
