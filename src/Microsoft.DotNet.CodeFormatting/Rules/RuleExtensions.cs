// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    public static class RuleExtensions
    {
        public static IEnumerable<SyntaxTrivia> AddTwoNewLines(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed });
        }

        public static IEnumerable<SyntaxTrivia> AddNewLine(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed });
        }

        public static IEnumerable<SyntaxTrivia> AddWhiteSpaceTrivia(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.Tab });
        }
    }
}
