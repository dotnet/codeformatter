// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasNewLineBeforeFirstNamespaceFormattingRuleTests : CodeFormattingTestBase
    {
        [Fact]
        public void TestNewLineBeforeFirstNamespace01()
        {
            var text = @"
using System;
//some comment
#if true
namespace N { }
#endif
";
            var expected = @"
using System;
//some comment

#if true
namespace N { }
#endif
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace02()
        {
            var text = @"
using System;
/*some comment1
some comment2
*/
namespace N { }
";
            var expected = @"
using System;
/*some comment1
some comment2
*/

namespace N { }
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace03()
        {
            var text = @"
using System;
#pragma warning disable 1591
namespace N { }
";
            var expected = @"
using System;

#pragma warning disable 1591
namespace N { }
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace04()
        {
            var text = @"
// some comment
#pragma warning disable 1561
#if true
#endif
namespace N { }
";
            var expected = @"
// some comment

#pragma warning disable 1561
#if true
#endif
namespace N { }
";
            Verify(text, expected);
        }

        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.HasNewLineBeforeFirstNamespaceFormattingRule();
        }
    }
}
