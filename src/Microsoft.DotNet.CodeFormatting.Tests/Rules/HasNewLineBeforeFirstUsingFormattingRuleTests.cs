// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasNewLineBeforeFirstUsingFormattingRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.HasNewLineBeforeFirstUsingFormattingRule(); }
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing01()
        {
            var text = @"
//some comment
using System;
";
            var expected = @"
//some comment

using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing02()
        {
            var text = @"
/*some comment1
some comment2
*/
using System;
";
            var expected = @"
/*some comment1
some comment2
*/

using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing03()
        {
            var text = @"
// copyright comment
#pragma warning disable 1591
using System;
";
            var expected = @"
// copyright comment

#pragma warning disable 1591
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing04()
        {
            var text = @"
// some comment
#pragma warning disable 1561
#if true
using System;
#endif
";
            var expected = @"
// some comment

#pragma warning disable 1561
#if true
using System;
#endif
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing05()
        {
            var text = @"
// some comment
#if true
#endif
using System;
";
            var expected = @"
// some comment

#if true
#endif
using System;
";
            Verify(text, expected);
        }
    }
}
