// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class NonAsciiCharactersAreEscapedInLiteralsTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.NonAsciiCharactersAreEscapedInLiterals(); }
        }

        [Fact]
        public void CanUseNonAsciiCharactersInComments()
        {
            var text = string.Format(@"
// It's oaky to use non ASCII characters like {0} (CHECK MARK U+2713) or {1} (RAINBOW U+1F308) in comments.
/*
It's oaky to use non ASCII characters like {0} (CHECK MARK U+2713) or {1} (RAINBOW U+1F308) in comments.
*/
", '\u2713', "\U0001F308");
            var expected = text;

            Verify(text, expected);
        }

        [Fact]
        public void DoNotAllowUnicodeInLiterals()
        {
            var text = string.Format(@"
using System;

class Test
{{
    public static readonly string BadString = ""This has {0} and {1}, which are both bad."";
    public static readonly string AnotherBadString = @""This has {0} and {1}, which are both bad, but we don't rewrite yet."";
    public const char BadChar = '{0}';
}}
", '\u2713', "\U0001F308");

            var expected = string.Format(@"
using System;

class Test
{{
    public static readonly string BadString = ""This has \u2713 and \U0001F308, which are both bad."";
    public static readonly string AnotherBadString = @""This has {0} and {1}, which are both bad, but we don't rewrite yet."";
    public const char BadChar = '\u2713';
}}
", '\u2713', "\U0001F308");

            Verify(text, expected);
        }

        [Fact]
        public void NonAsciiRewriterHandlesNonStringLiterals()
        {
            var text = @"
using System;

class Test
{
    public const int OkayInt = 12345;
}
";

            Verify(text, text);
        }
    }
}
