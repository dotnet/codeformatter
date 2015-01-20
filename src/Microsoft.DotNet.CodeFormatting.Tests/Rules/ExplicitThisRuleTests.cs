using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitThisRuleTests : CodeFormattingTestBase
    {
        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.ExplicitThisRule();
        }

        [Fact]
        public void TestFieldUse()
        {
            var text = @"
class C1
{
    int _field1;
    string _field2;
    string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(this._field1);
        Use(this._field2);
        Use(this.field3);
    }
}
";

            var expected = @"
class C1
{
    int _field1;
    string _field2;
    string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(_field1);
        Use(_field2);
        Use(this.field3);
    }
}
";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldAssignment()
        {
            var text = @"
class C1
{
    int _field1;
    string _field2;
    string field3;

    void M()
    {
        this._field1 = 0;
        this._field2 = null;
        this.field3 = null;
    }
}
";

            var expected = @"
class C1
{
    int _field1;
    string _field2;
    string field3;

    void M()
    {
        _field1 = 0;
        _field2 = null;
        this.field3 = null;
    }
}
";
            Verify(text, expected, runFormatter: false);
        }
    }
}
