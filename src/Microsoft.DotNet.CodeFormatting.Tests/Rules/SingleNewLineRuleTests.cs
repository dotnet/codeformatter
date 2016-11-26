// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class SingleNewLineRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get
            {
                return new Rules.SingleNewLineRule();
            }
        }

        [Fact]
        public void SimpleNamespace()
        {
            var text = @"namespace A
{


}";
            var expected = @"namespace A
{

}";
            Verify(text, expected);
        }

        [Fact]
        public void BeforeComment()
        {
            var text = @"class A
{


// comment
}";
            var expected = @"class A
{

    // comment
}";
            Verify(text, expected);
        }

        [Fact]
        public void AfterComment()
        {
            var text = @"class A
{
    // comment



}";
            var expected = @"class A
{
    // comment

}";
            Verify(text, expected);
        }

        [Fact]
        public void CommentFollowedBySingleLine()
        {
            var text = @"class A
{
    // comment

}";
            Verify(text, text);
        }

        [Fact]
        public void DirectiveFollowedBySingleLine()
        {
            var text = @"class A
{
#pragma warning disable 1591

}";
            Verify(text, text);
        }

        [Fact]
        public void NewLineBetweenComments()
        {
            var text = @"class A
{
    // comment

    // comment2
}";
            Verify(text, text);
        }
    }
}
