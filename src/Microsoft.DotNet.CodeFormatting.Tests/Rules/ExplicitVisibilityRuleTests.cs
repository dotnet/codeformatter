using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitVisibilityRuleTests : CodeFormattingTestBase
    {
        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.ExplicitVisibilityRule();
        }

        [Fact]
        public void TestTypeVisibility()
        {
            var text = @"
class C1 { } 
sealed partial class C2 { } 
struct S1 { }
interface I1 { } 
enum E1 { }
delegate void D1() { } 
";

            var expected = @"
internal class C1 { } 
internal sealed partial class C2 { } 
internal struct S1 { }
internal interface I1 { } 
internal enum E1 { }
internal delegate void D1() { } 
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestNestedTypeVisibility()
        {
            var text = @"
class C
{
    class C { } 
    struct S { } 
    interface I { } 
    enum E { } 
    delegate void D() { } 
}
";

            var expected = @"
internal class C
{
    private class C { } 
    private struct S { } 
    private interface I { } 
    private enum E { } 
    private delegate void D() { } 
}
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestMethodVisibility()
        {
            var text = @"
internal class C
{
    void M1();
    internal void M2();
}

internal struct S
{
    void M1();
    internal void M2();
}

internal interface I
{
    void M1();
    void M2();
}
";

            var expected = @"
internal class C
{
    private void M1();
    internal void M2();
}

internal struct S
{
    private void M1();
    internal void M2();
}

internal interface I
{
    void M1();
    void M2();
}
";

                Verify(text, expected, runFormatter: false);
        }
    }
}
