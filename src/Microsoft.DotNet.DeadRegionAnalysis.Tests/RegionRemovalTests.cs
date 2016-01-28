// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class RegionRemovalTests : TestBase
    {
        [Fact]
        public void RemoveDisabledIf()
        {
            var source = @"
// Test
#if false
class VARYING {}
#endif
// B
class B {}
";
            var expected = @"
// Test
// B
class B {}
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveDisabledIfWithWhitespaceTrivia()
        {
            var source = @"
class A
{
    // F
    #if false
    public int F;
    #endif // if F

    // G
    public int G;
}
";

            var expected = @"
class A
{
    // F

    // G
    public int G;
}
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveDisabledIfWithElse()
        {
            var source = @"
// Test
#if false
// A
class A {}
#else
// B
class B {}
#endif // if false
// End Test
";
            var expected = @"
// Test
// B
class B {}
// End Test
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveDisabledElse()
        {
            var source = @"
// Test
#if true
// A
class A {}
#else
// B
class B {}
#endif // if true
// End Test
";
            var expected = @"
// Test
// A
class A {}
// End Test
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveDisabledIfWithVaryingElifElse()
        {
            // Replace elifs where the previous region is an if
            var source = @"
// Test
#if false
// A
class A {}
#elif VARYING // !false
// B
class B {}
#else // !false && !VARYING == VARYING
// C
class C {}
#endif // if false
// End Test
";
            var expected = @"
// Test
#if VARYING // !false
// B
class B {}
#else // !false && !VARYING == VARYING
// C
class C {}
#endif // if false
// End Test
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveDisabledAdjacentUnrelatedRegions()
        {
            var source = @"
class C
{
// Test
#if true
    int A;
#else
    int B;
#endif // if true

#if false
    int D;
#elif VARYING // !false
    int E;
#else // !false && !VARYING
    int F;
#endif
// End Test
}
";
            var expected = @"
class C
{
// Test
    int A;

#if VARYING // !false
    int E;
#else // !false && !VARYING
    int F;
#endif
// End Test
}
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveEnabledIf()
        {
            var source = @"
// Test
#if true // true
// A
class A {}
#endif // if true
// End Test
";
            var expected = @"
// Test
// A
class A {}
// End Test
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveEnabledIfWithChain()
        {
            var source = @"
#if true
class A {}
#elif false
class B {}
#elif false
class C {}
#else // false
class D {}
#endif
";
            var expected = @"
class A {}
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveEnabledElifWithChain()
        {
            var source = @"
#if false
class A {}
#elif true
class B {}
#elif false
class C {}
#else // !false
class D {}
#endif
";
            var expected = @"
class B {}
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveEnabledElseWithChain()
        {
            var source = @"
#if false
class A {}
#elif false
class B {}
#elif false
class C {}
#else // true
class D {}
#endif
";
            var expected = @"
class D {}
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveIfAndElifWithVaryingChain()
        {
            var source = @"
#if false
class A {}
#elif false
class B {}
#elif VARYING
class C {}
#else // !VARYING
class D {}
#endif
";
            var expected = @"
#if VARYING
class C {}
#else // !VARYING
class D {}
#endif
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveStaggeredElifs()
        {
            var source = @"
#if false
class A {}
#elif VARYING
class B {}
#elif false // !VARYING && false == false
class C {}
#elif VARYING // !false && VARYING == VARYING
class D {}
#elif false // !VARYING && false == false
class E {}
#endif
";
            var expected = @"
#if VARYING
class B {}
#elif VARYING // !false && VARYING == VARYING
class D {}
#endif
";
            Verify(source, expected);
        }

        [Fact]
        public void RemoveNestedRegions()
        {
            var source = @"
#if false
  #if false
    #if VARYING
    #endif
  #else
    // True
  #endif
#endif
";
            var expected = @"
";
            Verify(source, expected);
        }

        protected void Verify(string source, string expected, bool runFormatter = true)
        {
            var inputSolution = CreateSolution(new[] { source });
            var expectedSolution = CreateSolution(new[] { expected });

            var engine = AnalysisEngine.FromProjects(inputSolution.Projects, alwaysIgnoredSymbols: new[] { "VARYING" });
            var regionInfo = engine.GetConditionalRegionInfo().Result.Single();
            var actualSolution = engine.RemoveUnnecessaryRegions(regionInfo).Result.Project.Solution;

            AssertSolutionEqual(expectedSolution, actualSolution);
        }
    }
}
