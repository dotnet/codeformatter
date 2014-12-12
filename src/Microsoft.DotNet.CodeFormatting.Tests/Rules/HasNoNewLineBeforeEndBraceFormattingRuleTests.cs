// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasNoNewLineBeforeEndBraceFormattingRuleTests : CodeFormattingTestBase
    {
        [Fact]
        public void TestNoNewLineBeforeEndBrace01()
        {
            var text = @"
using System;
class T
{
   
}
class K { }
class R {
    // some comment
}
class U
{

// some comment


#pragma warning disable 1591

}
class O 
{ // some comment

}
class M {
}";
            var expected = @"
using System;
class T
{
}
class K { }
class R
{
    // some comment
}
class U
{
    // some comment


#pragma warning disable 1591
}
class O
{ // some comment
}
class M
{
}";
            Verify(text, expected);
        }

        [Fact]
        public void TestNoNewLineBeforeEndBrace02()
        {
            var text = @"
class S {

/* some comment */ }
class L { // some comment

}
";
            var expected = @"
class S
{
    /* some comment */
}
class L
{ // some comment
}
";
            Verify(text, expected);
        }

        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.HasNoNewLineBeforeEndBraceFormattingRule();
        }
    }
}
