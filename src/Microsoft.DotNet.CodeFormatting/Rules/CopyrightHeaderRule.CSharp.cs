// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    internal partial class CopyrightHeaderRule
    {
        private sealed class CSharpRule : CommonRule
        {
            internal CSharpRule(ImmutableArray<string> header) : base(header)
            {
            }

            protected override SyntaxTriviaList CreateTriviaList(IEnumerable<SyntaxTrivia> e)
            {
                return SyntaxFactory.TriviaList(e);
            }

            protected override bool IsLineComment(SyntaxTrivia trivia)
            {
                return trivia.Kind() == SyntaxKind.SingleLineCommentTrivia;
            }

            protected override bool IsWhitespace(SyntaxTrivia trivia)
            {
                return trivia.Kind() == SyntaxKind.WhitespaceTrivia;
            }

            protected override bool IsNewLine(SyntaxTrivia trivia)
            {
                return trivia.Kind() == SyntaxKind.EndOfLineTrivia;
            }

            protected override SyntaxTrivia CreateLineComment(string commentText)
            {
                return SyntaxFactory.Comment("// " + commentText);
            }

            protected override SyntaxTrivia CreateNewLine()
            {
                return SyntaxFactory.CarriageReturnLineFeed;
            }
        }
    }
}
