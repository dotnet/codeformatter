using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class CopyrightHeaderRuleTests : SyntaxRuleTestBase
    {
        private readonly Options _options = new Options();

        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.CopyrightHeaderRule(_options); }
        }

        [Fact]
        public void CSharpSimple()
        {
            _options.CopyrightHeader = ImmutableArray.Create("test");
            var source = @"
class C
{
}";

            var expected = @"// test

class C
{
}";
            Verify(source, expected);

        }

        [Fact]
        public void CSharpPreserveExisting()
        {
            _options.CopyrightHeader = ImmutableArray.Create("test");
            var source = @"// test

class C
{
}";

            var expected = @"// test

class C
{
}";
            Verify(source, expected);

        }

        [Fact]
        public void CSharpDontDoubleComment()
        {
            _options.CopyrightHeader = ImmutableArray.Create("// test");
            var source = @"
class C
{
}";

            var expected = @"// test

class C
{
}";
            Verify(source, expected);
        }

        [Fact]
        public void VisualBasicSimple()
        {
            _options.CopyrightHeader = ImmutableArray.Create("test");
            var source = @"
Public Class C
End Class";

            var expected = @"' test

Public Class C
End Class";

            Verify(source, expected, languageName: LanguageNames.VisualBasic);
        }

        [Fact]
        public void VisualBasicNormalizeComment()
        {
            _options.CopyrightHeader = ImmutableArray.Create("// test");
            var source = @"
Public Class C
End Class";

            var expected = @"' test

Public Class C
End Class";

            Verify(source, expected, languageName: LanguageNames.VisualBasic);
        }

        [Fact]
        public void VisualBasicPreserveExisting()
        {
            _options.CopyrightHeader = ImmutableArray.Create("// test");
            var source = @"' test

Public Class C
End Class";

            var expected = @"' test

Public Class C
End Class";

            Verify(source, expected, languageName: LanguageNames.VisualBasic);
        }
    }
}
