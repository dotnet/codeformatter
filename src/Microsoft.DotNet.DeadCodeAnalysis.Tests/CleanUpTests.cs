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
            var markup = @"
// Test
$$#if A
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
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledIfWithWhitespaceTrivia()
        {
            var markup = @"
class A
{
    // F
    $$#if F
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
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledIfWithElse()
        {
            var markup = @"
// Test
$$#if A
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
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledElse()
        {
            var markup = @"
// Test
#if A
// A
class A {}
$$#else
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
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledIfWithElifElse()
        {
            var markup = @"
// Test
$$#if A
// A
class A {}
#elif B // !A
// B
class B {}
#else // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            var expected = @"
// Test
#if B // !A
// B
class B {}
#else // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledElifWithIfElse()
        {
            var markup = @"
// Test
#if A
// A
class A {}
$$#elif B // !A
// B
class B {}
#else // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            var expected = @"
// Test
#if A
// A
class A {}
#else // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledElseWithElif()
        {
            var markup = @"
// Test
#if A
// A
class A {}
#elif B // !A
// B
class B {}
$$#else // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            var expected = @"
// Test
#if A
// A
class A {}
$$#elif B // !A
// B
class B {}
#endif // if A
// End Test
";
        }

        [Fact]
        public void RemoveDisabledStaggeredElifs()
        {
            var markup = @"
// Test
#if A
// A
class A {}
$$#elif B // !A
// B
class B {}
#elif C // !A && !B
// C
class C {}
$$#elif D // !A && !B && !C
// D
class D {}
#endif // if A
// End Test
";
            var expected = @"
// Test
#if A
// A
class A {}
#elif C // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledStaggeredIfAndElifs()
        {
            var markup = @"
// Test
$$#if A
// A
class A {}
$$#elif B // !A
// B
class B {}
#elif C // !A && !B
// C
class C {}
$$#elif D // !A && !B && !C
// D
class D {}
#endif // if A
// End Test
";
            var expected = @"
// Test
#if C // !A && !B
// C
class C {}
#endif // if A
// End Test
";
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledAdjacentUnrelatedRegions()
        {
            var markup = @"
class C
{
// Test
#if A
    int A;
$$#else B // !A
    int B;
#endif // if A

$$#if D
    int D;
#elif E // !D
    int E;
#else // !E
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

#if E // !D
    int E;
#else // !E
    int F;
#endif
// End Test
}
";
            Verify(markup, expected);
        }

        [Fact]
        public void RemoveDisabledAdjacentUnrelatedRegions2()
        {
            var markup = @"
class C
{
// Test
$$#if A
    int A;
#endif // if A

$$#if D
    int D;
#elif E // !D
    int E;
#else // !E
    int F;
#endif
// End Test
}
";
            var expected = @"
class C
{
// Test

#if E // !D
    int E;
#else // !E
    int F;
#endif
// End Test
}
";
            Verify(markup, expected);
        }

        


        public void RemoveActiveIf()
        {
            var markup = @"
#if A // A
class A {}
#endif // if A
";
            var expected = @"
class A {}
";
        }

        public void RemoveComplex()
        {
            var markup = @"
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
        }

        public void RemoveComplex2()
        {
            var markup = @"
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
        }

        public void RemoveComplex3()
        {
            var markup = @"
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
        }

        // TODO: Prune these based on knowledge
        // Note: these keep varying cases are the same thing as the removal cases
        // What I can't wrap my head around is, can I prove that if I remove something, the remaining regions are valid?
        // If you remove a region that is always false, the next region in the chain depends on !false && existing_condition.
        // So that's totally fine.
        // If you remove a region that is always active, the next region depends on !true && existing_condition.
        // So the following regions must be inactive...
        //   BUT, if the following regions are explicitly ignored,
        public void KeepVarying1()
        {
            var markup = @"
#if A // Varying
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
#if A // Varying
class A {}
#endif
";
        }

        public void KeepVarying2()
        {
            var markup = @"
#if false
class A {}
#elif B // Varying
class B {}
#elif false
class C {}
#else // false
class D {}
#endif
";
            var expected = @"
#if B // Varying
class B {}
#endif
";
        }

        public void KeepVarying3()
        {
            var markup = @"
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
        }

        private static async Task<Document> RewriteDocumentAsync(Document document, bool runFormatter)
        {
            // TODO: Run clean up

            if (runFormatter)
            {
                return await Formatter.FormatAsync(document, null, CancellationToken.None);
            }

            return document;
        }

        private static async Task<Document> RemoveDisabledDirectives(Document document, IEnumerable<int> positions, CancellationToken cancellationToken)
        {
            //var directivesToRemove = await GetDirectivesFromPositions(document, positions, cancellationToken);
            //return await CleanUp.RemoveInactiveDirectives(document, directivesToRemove, cancellationToken);

            // TODO: Fix this and update tests
            throw new NotImplementedException();
        }

        protected void Verify(string markup, string expected, bool runFormatter = true)
        {
            string source;
            var positions = GetPositions(markup, out source);

            var inputSolution = CreateSolution(new[] { source });
            var expectedSolution = CreateSolution(new[] { expected });

            var inputDocument = inputSolution.Projects.Single().Documents.Single();
            var actualDocument = RemoveDisabledDirectives(inputDocument, positions, CancellationToken.None).Result;
            var actualSolution = actualDocument.Project.Solution;

            AssertSolutionEqual(expectedSolution, actualSolution);
        }
    }
}
