using Microsoft.CodeAnalysis;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.DeadCodeAnalysis.Tests
{
    public class AnalysisTests : TestBase
    {
        [Fact]
        public void IdentifySharedIf()
        {
            var markup = @"
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

            Verify(markup, expectedStates);
        }

        [Fact]
        public void IdentifySharedElse()
        {
            var markup = @"
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

            Verify(markup, expectedStates);
        }

        [Fact]
        public void IdentifySharedElif()
        {
            var markup = @"
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

            Verify(markup, expectedStates);
        }

        [Fact]
        public void IdentifySharedNestedIf()
        {
            var markup = @"
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

            Verify(markup, expectedStates);
        }

        [Fact]
        public void IdentifySharedNestedDisabledIfs()
        {
            var markup = @"
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
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysDisabled,
                ConditionalRegionState.AlwaysDisabled
            };

            Verify(markup, expectedStates);
        }

        // TODO: Add a test case which would fail if nested regions were AFTER the outer region.  Probably a removal test because it would screw up the spans.
        // TODO: Test case to make sure regions which are not shared are not included

        private static readonly string[] s_defaultPreprocessorSymbolsA = new[] { "A" };
        private static readonly string[] s_defaultPreprocessorSymbolsB = new[] { "B" };

        private void Verify(string markup, ConditionalRegionState[] expectedStates, string[] preprocessorSymbolsA = null, string[] preprocessorSymbolsB = null)
        {
            if (preprocessorSymbolsA == null)
            {
                preprocessorSymbolsA = s_defaultPreprocessorSymbolsA;
            }

            if (preprocessorSymbolsB == null)
            {
                preprocessorSymbolsB = s_defaultPreprocessorSymbolsB;
            }

            string source;
            var positions = GetPositions(markup, out source);

            var projectA = CreateSolution(new[] { source }, preprocessorSymbolsA).Projects.Single();
            var projectB = CreateSolution(new[] { source }, preprocessorSymbolsB).Projects.Single();

            // TODO: Fix this and update tests
            //var info = Analysis.GetIntersectedConditionalRegionInfo(new[] { projectA, projectB }).Result;
            //Assert.Equal(1, info.Length);

            // TODO: Eventually we will just get a flat list of regions, so there will be no need for chains here.
            //var regions = info[0].Regions.SelectMany(c => c.Regions).ToArray();
            //Assert.Equal(expectedStates.Length, regions.Length);

            //// Make sure the state of each region is what we expect
            //for (int i = 0; i < expectedStates.Length; i++)
            //{
            //    var expectedState = expectedStates[i];
            //    var region = regions[i];
            //    if (expectedState != region.State)
            //    {
            //        Assert.False(true, string.Format("The state of the region on line {0} is {1}, expected {2}: {3}",
            //            region.Location.GetLineSpan().StartLinePosition.Line,
            //            region.State,
            //            expectedState,
            //            region.StartDirective.ToFullString()));
            //    }
            //}
        }
    }
}
