using Microsoft.CodeAnalysis;
using System;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class RegionAnalysisTests : TestBase
    {
        [Fact]
        public void IdentifySharedIf()
        {
            var source = @"
#if true
#endif

#if A
#endif
";
            var expectedStates = new[]
            {
                ConditionalRegionState.AlwaysEnabled,
                ConditionalRegionState.Varying
            };

            Verify(source, expectedStates);
        }

        [Fact]
        public void IdentifySharedElse()
        {
            var source = @"
#if false
#else
#endif

#if B
#endif
";
            var expectedStates = new[]
            {
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysEnabled,
                ConditionalRegionState.Varying
            };

            Verify(source, expectedStates);
        }

        [Fact]
        public void IdentifySharedElif()
        {
            var source = @"
#if false
#elif A || B
#endif

#if C
#endif
";
            var expectedStates = new[]
            {
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysEnabled,
                ConditionalRegionState.AlwaysDisabled
            };

            Verify(source, expectedStates);
        }

        [Fact]
        public void IdentifySharedNestedIf()
        {
            var source = @"
#if true
    #if A || B
    #endif
    #if false
    #endif
#endif

#if C
#endif
";
            var expectedStates = new[]
            {
                ConditionalRegionState.AlwaysEnabled,
                ConditionalRegionState.AlwaysEnabled,
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysDisabled
            };

            Verify(source, expectedStates);
        }

        [Fact]
        public void IdentifySharedNestedDisabledIfs()
        {
            var source = @"
#if false
    #if false
    #endif
    #if true
        #if A
        #elif B
        #else
        #endif
    #endif
#endif
";
            var expectedStates = new[]
            {
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysEnabled,
                ConditionalRegionState.Varying,
                ConditionalRegionState.Varying,
                ConditionalRegionState.Varying
            };

            Verify(source, expectedStates);
        }

        private static readonly string[] s_defaultPreprocessorSymbolsA = new[] { "A" };
        private static readonly string[] s_defaultPreprocessorSymbolsB = new[] { "B" };

        private void Verify(string source, ConditionalRegionState[] expectedStates, string[] preprocessorSymbolsA = null, string[] preprocessorSymbolsB = null)
        {
            if (preprocessorSymbolsA == null)
            {
                preprocessorSymbolsA = s_defaultPreprocessorSymbolsA;
            }
            if (preprocessorSymbolsB == null)
            {
                preprocessorSymbolsB = s_defaultPreprocessorSymbolsB;
            }

            var projectA = CreateSolution(new[] { source }, preprocessorSymbolsA).Projects.Single();
            var projectB = CreateSolution(new[] { source }, preprocessorSymbolsB).Projects.Single();
            var engine = AnalysisEngine.FromProjects(new[] { projectA, projectB });

            var regionInfo = engine.GetConditionalRegionInfo().Result.Single();
            var regions = regionInfo.Chains.SelectMany(c => c.Regions).ToArray();
            Array.Sort(regions);

            Assert.Equal(expectedStates.Length, regions.Length);

            // Make sure the state of each region is what we expect
            for (int i = 0; i < expectedStates.Length; i++)
            {
                var expectedState = expectedStates[i];
                var region = regions[i];
                if (expectedState != region.State)
                {
                    Assert.False(true, string.Format("The state of the region on line {0} is {1}, expected {2}: {3}",
                        region.Location.GetLineSpan().StartLinePosition.Line,
                        region.State,
                        expectedState,
                        region.StartDirective.ToFullString()));
                }
            }
        }
    }
}
