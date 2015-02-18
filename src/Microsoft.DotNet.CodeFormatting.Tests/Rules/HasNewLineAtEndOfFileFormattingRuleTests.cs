// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasNewLineAtEndOfFileFormattingRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.HasNewLineAtEndOfFileFormattingRule(); }
        }

        [Fact]
        public void ShouldAddNewLine()
        {
            var source = "public class C { }";

            var expected = "public class C { }\r\n";

            Verify(source, expected);
        }

        [Fact]
        public void ShouldRemoveExtraNewLines()
        {
            var source = "public class C { }\r\n\r\n\n\r\n\n\r";

            var expected = "public class C { }\r\n";

            Verify(source, expected);
        }

        [Fact]
        public void ShouldNotCareAboutExistingCarriageReturn()
        {
            var source = "public class C { }\r";

            var expected = "public class C { }\r\n";

            Verify(source, expected);
        }

        [Fact]
        public void ShouldNotCareAboutExistingLineFeed()
        {
            var source = "public class C { }\n";

            var expected = "public class C { }\r\n";

            Verify(source, expected);
        }

        [Fact]
        public void ShouldHandleEmptyDocument()
        {
            var source = string.Empty;

            var expected = "\r\n";

            Verify(source, expected);
        }
    }
}