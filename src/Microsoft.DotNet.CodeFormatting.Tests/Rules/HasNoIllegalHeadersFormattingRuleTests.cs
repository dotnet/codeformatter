// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasNoIllegalHeadersFormattingRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.HasNoIllegalHeadersFormattingRule(); }
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule01()
        {
            var text = @"
// <OWNER>test</OWNER>
using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule02()
        {
            var text = @"
// <OWNER>test</OWNER>
// <Owner>foobar</owner>
using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule03()
        {
            var text = @"
/* <OWNER>test</OWNER>
* <Owner>foobar</owner>
*/
using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule04()
        {
            var text = @"
/* 
* <OWNER>test</OWNER>
* <Owner>foobar</owner>
*/
// Foobar
using System;
";
            var expected = @"
// Foobar
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule05()
        {
            var text = @"
/* This is important stuff
* <OWNER>test</OWNER>
* <Owner>foobar</owner>
*/
// Foobar
using System;
";
            var expected = @"
/* This is important stuff
*/
// Foobar
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule06()
        {
            var text = @"
/* 
* <OWNER>test</OWNER>
* <Owner>foobar</owner>
This is important stuff */
// Foobar
using System;
";
            var expected = @"
/* 
This is important stuff */
// Foobar
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule07()
        {
            var text = @"
/* 
This is important stuff 

Please keep this
*/

using System;
";
            var expected = @"
/* 
This is important stuff 

Please keep this
*/

using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule08()
        {
            var text = @"
/* 
*/

using System;
";
            var expected = @"
/* 
*/

using System;
";
            Verify(text, expected);
        }
        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule09()
        {
            var text = @"/* <owner> foo </owner> */
/* <owner> bar </owner> */
/* <owner> baz </owner> */

using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule10()
        {
            var text = @"/* <owner> bar </owner> 
 <owner> baz </owner> */

using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule11()
        {
            var text = @"// ==++== bar ==++== 
 /* <owner> baz </owner> */

using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule12()
        {
            var text = @"// ==++== bar ==++== 
/* <owner> baz </owner> */
// ==++== bar ==++== 
/* <owner> baz </owner> */
// <owner> baz </owner>

using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule13()
        {
            var text = @"
/* ============================
    <owner> foobar</owner>
      This is important
   ============================ */

using System;
";
            var expected = @"
/* ============================
      This is important
   ============================ */

using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestHasNoIllegalHeadersFormattingRule14()
        {
            var text = @"
/* ============================
    Test0.cs
   ============================ */
using System;
";
            var expected = @"
using System;
";
            Verify(text, expected);
        }
    }
}
