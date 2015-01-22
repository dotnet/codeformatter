// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class BraceNewLineTests : CodeFormattingTestBase
    {
        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.BraceNewLineRule();
        }

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
}
class R
{
    // some comment

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
}
class R
{
    // some comment
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

        [Fact]
        public void TestNoNewLineBeforeEndBrace03()
        {
            var text = @"
class S 
{


    }    

  
";
            var expected = @"
class S
{
}


";
            Verify(text, expected);
        }

        [Fact]
        public void TestNoNewLineBeforeEndBrace04()
        {
            var text = @"
class S 
{   
    

}";
            var expected = @"
class S
{
}";
            Verify(text, expected);
        }

        [Fact]
        public void TestRemoveNewLinesBetweenPragmaAndCloseBrace()
        {
            var text = @"
class U
{

// some comment


#pragma warning disable 1591

}
";
            var expected = @"
class U
{
    // some comment


#pragma warning disable 1591
}
";

            Verify(text, expected);
        }

        [Fact]
        public void TestBracesWithJustCommentBody()
        {
            var text = @"
class U
{
// some comment
}
";
            var expected = @"
class U
{
    // some comment
}
";

            Verify(text, expected);
        }

        [Fact]
        public void MethodWithSingleLine()
        {
            var text = @"
class U
{
    void M()
    {
        N();
    }
}
";
            var expected = @"
class U
{
    void M()
    {
        N();
    }
}
";

            Verify(text, expected);

        }

        [Fact]
        public void NewLineBeforeCloseBraceOnClass()
        {
            var text = @"
class C { 
}
";
            var expected = @"
class C
{
}
";
            Verify(text, expected);
        }

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
    }
}
