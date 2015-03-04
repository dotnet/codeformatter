// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public partial class AnalysisEngine
    {
        internal class Options
        {
            private ImmutableArray<ImmutableDictionary<string, Tristate>> _symbolConfigurations;
            private Tristate _undefinedSymbolValue;

            public ImmutableArray<Document> Documents { get; private set; }

            public IAnalysisLogger Logger { get; set; }

            internal Options(
                IEnumerable<Project> projects = null,
                IEnumerable<string> projectPaths = null,
                IEnumerable<string> sourcePaths = null,
                IEnumerable<IEnumerable<string>> symbolConfigurations = null,
                IEnumerable<string> alwaysIgnoredSymbols = null,
                IEnumerable<string> alwaysDefinedSymbols = null,
                IEnumerable<string> alwaysDisabledSymbols = null,
                Tristate undefinedSymbolValue = default(Tristate),
                IAnalysisLogger logger = null)
            {
                if (projectPaths != null)
                {
                    projects = Task.WhenAll(from path in projectPaths select MSBuildWorkspace.Create().OpenProjectAsync(path, CancellationToken.None)).Result;
                }
                if (projects != null)
                {
                    Documents = GetSharedDocuments(projects);
                }

                if (projects == null && sourcePaths != null)
                {
                    var projectId = ProjectId.CreateNewId("AnalysisProject");
                    var solution = new AdhocWorkspace()
                        .CurrentSolution
                        .AddProject(projectId, "AnalysisProject", "AnalysisProject", LanguageNames.CSharp);

                    foreach (var path in sourcePaths)
                    {
                        var documentId = DocumentId.CreateNewId(projectId);
                        solution = solution.AddDocument(
                            documentId,
                            Path.GetFileName(path),
                            new FileTextLoader(path, defaultEncoding: Encoding.UTF8));
                    }

                    Documents = solution.Projects.Single().Documents.ToImmutableArray();
                }

                _symbolConfigurations = CalculateSymbolConfigurations(
                    alwaysDisabledSymbols,
                    alwaysDefinedSymbols,
                    alwaysIgnoredSymbols,
                    symbolConfigurations);

                _undefinedSymbolValue = undefinedSymbolValue;

                Logger = logger ?? new ConsoleAnalysisLogger();
            }

            internal CompositePreprocessorExpressionEvaluator GetPreprocessorExpressionEvaluator()
            {
                var evaluators = _symbolConfigurations.Select(config => new PreprocessorExpressionEvaluator(config, _undefinedSymbolValue));
                return new CompositePreprocessorExpressionEvaluator(evaluators);
            }

            internal PreprocessorSymbolTracker GetPreprocessorSymbolTracker()
            {
                var specifiedSymbols = new HashSet<string>(StringComparer.Ordinal);

                foreach (var config in _symbolConfigurations)
                {
                    foreach (string symbol in config.Keys)
                    {
                        specifiedSymbols.Add(symbol);
                    }
                }

                return new PreprocessorSymbolTracker(specifiedSymbols);
            }

            private static ImmutableArray<Document> GetSharedDocuments(IEnumerable<Project> projects)
            {
                using (var it = projects.GetEnumerator())
                {
                    if (!it.MoveNext())
                    {
                        return ImmutableArray<Document>.Empty;
                    }

                    var first = it.Current;
                    var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    do
                    {
                        foreach (var doc in it.Current.Documents)
                        {
                            filePaths.Add(doc.FilePath);
                        }
                    }
                    while (it.MoveNext());

                    return first.Documents.Where(d => filePaths.Contains(d.FilePath)).ToImmutableArray();
                }
            }

            private static ImmutableArray<ImmutableDictionary<string, Tristate>> CalculateSymbolConfigurations(
                IEnumerable<string> alwaysDisabledSymbols,
                IEnumerable<string> alwaysDefinedSymbols,
                IEnumerable<string> alwaysIgnoredSymbols,
                IEnumerable<IEnumerable<string>> symbolConfigurations)
            {
                var explicitStates = ImmutableDictionary.CreateBuilder<string, Tristate>();

                AddExplicitSymbolStates(explicitStates, alwaysDisabledSymbols, Tristate.False);
                AddExplicitSymbolStates(explicitStates, alwaysDefinedSymbols, Tristate.True);
                AddExplicitSymbolStates(explicitStates, alwaysIgnoredSymbols, Tristate.Varying);

                if (symbolConfigurations == null || !symbolConfigurations.Any())
                {
                    return ImmutableArray.Create(explicitStates.ToImmutable());
                }

                var configurationStateMaps = ImmutableArray.CreateBuilder<ImmutableDictionary<string, Tristate>>();
                foreach (var configuration in symbolConfigurations)
                {
                    var stateMap = ImmutableDictionary.CreateBuilder<string, Tristate>();

                    foreach (var item in explicitStates)
                    {
                        stateMap.Add(item);
                    }

                    foreach (var symbol in configuration)
                    {
                        if (!stateMap.ContainsKey(symbol))
                        {
                            stateMap.Add(symbol, Tristate.True);
                        }
                    }

                    configurationStateMaps.Add(stateMap.ToImmutable());
                }

                return configurationStateMaps.ToImmutable();
            }

            private static void AddExplicitSymbolStates(ImmutableDictionary<string, Tristate>.Builder symbolStates, IEnumerable<string> symbols, Tristate explicitState)
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
