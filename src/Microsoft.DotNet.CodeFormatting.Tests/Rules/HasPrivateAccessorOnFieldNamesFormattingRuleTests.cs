using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.CodeFormatting;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class CodeFormatterTests : CodeFormattingTestBase
    {
        [Fact]
        public void TestStatic()
        {
            var text = @"
using System;
class T
{
    static int x;
}";
            var expected = @"
using System;
class T
{
    private static int x;
}";
            Verify(text, expected);
        }

        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.HasPrivateAccessorOnFieldNamesFormattingRule();
        }
    }
}
