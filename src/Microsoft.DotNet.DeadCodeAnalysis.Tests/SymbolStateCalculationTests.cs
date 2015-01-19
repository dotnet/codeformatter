using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.DeadCodeAnalysis.Tests
{
    public class SymbolStateCalculationTests
    {
        [Fact]
        public void ExplicitValues()
        {
            Verify(
                new Dictionary<string, Tristate>()
                {
                    { "FALSE", Tristate.False },
                    { "TRUE", Tristate.True },
                    { "VARYING", Tristate.Varying }
                },
                alwaysDisabledSymbols: new[] { "FALSE" },
                alwaysDefinedSymbols: new[] { "TRUE" },
                alwaysIgnoredSymbols: new[] { "VARYING" });
        }

        [Fact]
        public void CalculatedValues()
        {
            Verify(
                new Dictionary<string, Tristate>()
                {
                    { "FALSE", Tristate.False },
                    { "TRUE", Tristate.True },
                    { "VARYING", Tristate.Varying }
                },
                symbolConfigurations: new[]
                {
                    new[] { "TRUE" },
                    new[] { "TRUE", "VARYING" }
                });
        }

        [Fact]
        public void OverrideCalculatedValues()
        {
            Verify(
                new Dictionary<string, Tristate>()
                {
                    { "FALSE", Tristate.False },
                    { "TRUE", Tristate.True },
                    { "VARYING", Tristate.Varying }
                },
                alwaysDisabledSymbols: new[] { "FALSE" },
                alwaysDefinedSymbols: new[] { "TRUE" },
                alwaysIgnoredSymbols: new[] { "VARYING" },
                symbolConfigurations: new[]
                {
                    new[] { "FALSE", "VARYING" }
                });
        }

        private static void Verify(
                Dictionary<string, Tristate> expectedStates,
                IEnumerable<string> alwaysDisabledSymbols = null,
                IEnumerable<string> alwaysDefinedSymbols = null,
                IEnumerable<string> alwaysIgnoredSymbols = null,
                IEnumerable<IEnumerable<string>> symbolConfigurations = null)
        {
            var actualStates = AnalysisEngine.Options.CalculateSymbolStates(
                alwaysDisabledSymbols,
                alwaysDefinedSymbols,
                alwaysIgnoredSymbols,
                symbolConfigurations);

            foreach (var item in expectedStates)
            {
                Tristate actualState;
                if (!actualStates.TryGetValue(item.Key, out actualState))
                {
                    actualState = Tristate.False;
                }
                Assert.Equal(item.Value, actualState);
            }
        }
    }
}
