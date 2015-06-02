using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class NewLineAtEndOfFileRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.NewLineAtEndOfFileRule(); }
        }

        [Fact]
        public void SimpleClassWithoutNewLineAtEndOfFile()
        {
            var text = @"using System;
public class TestClass
{
}";

            var expected = @"using System;
public class TestClass
{
}
";

            Verify(text, expected);
        }

        [Fact]
        public void SimpleClassWithNewLineAtEndOfFile()
        {
            var text = @"using System;
public class TestClass
{
}
";

            Verify(text, text);
        }

        [Fact]
        public void CommentAtEndOfFile()
        {
            var text = @"using System;
public class TestClass
{
}
//  Hello World";

            var expected = @"using System;
public class TestClass
{
}
//  Hello World
";

            Verify(text, expected);
        }

        [Fact]
        public void IfDefAtEndOfFile()
        {
            var text = @"using System;
public class TestClass
{
}
#if TEST
#endif";

            var expected = @"using System;
public class TestClass
{
}
#if TEST
#endif
";

            Verify(text, expected);
        }
    }
}
