// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class RegionAnalysisTests : TestBase
    {
        [Fact]
        public async Task IdentifySharedIfAsync()
        {
            var source = @"
#if true
#endif

#if A
#endif
";
            var expectedStates = new[]
            {
                Tristate.True,
                Tristate.Varying
            };

            await Verify(source, expectedStates);
        }

        [Fact]
        public async Task IdentifySharedElseAsync()
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
                Tristate.False,
                Tristate.True,
                Tristate.Varying
            };

            await Verify(source, expectedStates);
        }

        [Fact]
        public async Task IdentifySharedElifAsync()
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
                Tristate.False,
                Tristate.True,
                Tristate.False
            };

            await Verify(source, expectedStates);
        }

        [Fact]
        public async Task IdentifySharedNestedIfAsync()
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
                Tristate.True,
                Tristate.True,
                Tristate.False,
                Tristate.False
            };

            await Verify(source, expectedStates);
        }

        [Fact]
        public async Task IdentifySharedNestedDisabledIfsAsync()
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
                Tristate.False,
                Tristate.False,
                Tristate.True,
                Tristate.Varying,
                Tristate.Varying,
                Tristate.Varying
            };

            await Verify(source, expectedStates);
        }

        private static readonly string[] s_defaultPreprocessorSymbolsA = new[] { "A" };
        private static readonly string[] s_defaultPreprocessorSymbolsB = new[] { "B" };

        private async Task Verify(string source, Tristate[] expectedStates, string[] preprocessorSymbolsA = null, string[] preprocessorSymbolsB = null)
        {
            if (preprocessorSymbolsA == null)
            {
                preprocessorSymbolsA = s_defaultPreprocessorSymbolsA;
            }
            if (preprocessorSymbolsB == null)
            {
                preprocessorSymbolsB = s_defaultPreprocessorSymbolsB;
            }

            var projectA = CreateSolution(new[] { source }).Projects.Single();
            var engine = AnalysisEngine.FromProjects(
                new[] { projectA },
                symbolConfigurations: new[] { preprocessorSymbolsA, preprocessorSymbolsB });

            var regionInfo = (await engine.GetConditionalRegionInfo().ConfigureAwait(false)).Single();
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
