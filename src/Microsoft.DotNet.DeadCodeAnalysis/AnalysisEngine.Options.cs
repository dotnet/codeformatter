using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public partial class AnalysisEngine
    {
        public class Options
        {
            public IEnumerable<Project> Projects { get; private set; }

            public IEnumerable<string> ProjectPaths { get; private set; }

            public IEnumerable<string> SourcePaths { get; private set; }

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
                Projects = projects;
                ProjectPaths = projectPaths;
                SourcePaths = sourcePaths;

                SymbolStates = CalculateSymbolStates(
                    alwaysDisabledSymbols,
                    alwaysDefinedSymbols,
                    alwaysIgnoredSymbols,
                    symbolConfigurations);
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
