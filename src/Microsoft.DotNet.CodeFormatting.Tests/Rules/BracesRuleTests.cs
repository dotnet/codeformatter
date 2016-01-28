// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class BraceNewLineTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.BraceNewLineRule(); }
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
        public void TestEmptyBraceWhitespaceAfterOpen()
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
        public void TestEmptyBraceNoWhitespaceAfterOpen()
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
        public void TestBraceSingleMethodCall()
        {
            var text = @"
class S 
{
    void M()     
    {


        G();


    } 
}";
            var expected = @"
class S
{
    void M()
    {
        G();
    }
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

        /// <summary>
        /// This is a regression test for issue #36
        /// </summary>
        [Fact]
        public void CommentsBeforeCloseBraces()
        {
            var text = @"
class C
{
    void M()
    {
        if (b) {
            G();

            // A comment
        }
    }
}";

            var expected = @"
class C
{
    void M()
    {
        if (b)
        {
            G();

            // A comment
        }
    }
}";

            Verify(text, expected, runFormatter: true);
        }
    }
}
