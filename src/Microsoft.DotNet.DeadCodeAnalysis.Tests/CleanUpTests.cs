using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;
using System;

namespace Microsoft.DotNet.DeadCodeAnalysis.Tests
{
    public class CleanUpTests : TestBase
    {
        [Fact]
        public void RemoveDisabledIf()
        {
            var source = @"
// Test
#if false
class A {}
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
            var source = @"
// Test
#if false
// A
class A {}
#elif A // !false
// B
class B {}
#else // !false && !A
// C
class C {}
#endif // if false
// End Test
";
            var expected = @"
// Test
#if A // !false
// B
class B {}
#else // !false && !A
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
#elif A // !false
    int E;
#else // !false && !A
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

#if A // !false
    int E;
#else // !false && !A
    int F;
#endif
// End Test
}
";
            Verify(source, expected);
        }

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

        public void RemoveEnabledElifWithChain()
        {
            var source = @"
#if false
class A {}
#elif true
class B {}
#elif false
class C {}
#else // false
class D {}
#endif
";
            var expected = @"
class B {}
";
            Verify(source, expected);
        }

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

        public void RemoveIfAndElifWithVaryingChain()
        {
            var source = @"
#if false
class A {}
#elif false
class B {}
#elif C // Varying
class C {}
#else // Varying
class D {}
#endif
";
            var expected = @"
#if C // Varying
class C {}
#else // Varying
class D {}
#endif
";
            Verify(source, expected);
        }

        protected void Verify(string source, string expected, bool runFormatter = true)
        {
            var inputSolution = CreateSolution(new[] { source });
            var expectedSolution = CreateSolution(new[] { expected });

            var analysisEngine = new AnalysisEngine(AnalysisOptions.FromProjects(inputSolution.Projects, alwaysIgnoredSymbols: new[] { "A" }));
            var regionInfo = analysisEngine.GetConditionalRegionInfo().Result.Single();
            var actualSolution = analysisEngine.RemoveUnnecessaryRegions(regionInfo).Result.Project.Solution;

            AssertSolutionEqual(expectedSolution, actualSolution);
        }
    }
}
