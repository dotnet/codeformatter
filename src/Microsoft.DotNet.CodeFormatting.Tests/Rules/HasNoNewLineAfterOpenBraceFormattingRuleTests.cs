// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasNoNewLineAfterOpenBraceFormattingRuleTests : CodeFormattingTestBase
    {
        [Fact]
        public void TestNoNewLineAfterOpenBrace01()
        {
            var text = @"
using System;
class T
{
   
}
class K { }
class S {

}
class R 
{

    // some comment
}
class U
{

#pragma warning disable 1591
}
class L {
}";
            var expected = @"
using System;
class T
{

}
class K { }
class S
{
}
class R
{
    // some comment
}
class U
{
#pragma warning disable 1591
}
class L
{
}";
            Verify(text, expected);
        }

        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.HasNoNewLineAfterOpenBraceFormattingRule();
        }
    }
}
