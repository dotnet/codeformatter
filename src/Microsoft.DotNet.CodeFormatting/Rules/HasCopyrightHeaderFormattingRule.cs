// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRuleOrder(SyntaxRuleOrder.HasCopyrightHeaderFormattingRule)]
    internal sealed class HasCopyrightHeaderFormattingRule : ISyntaxFormattingRule
    {
        private static readonly string[] s_copyrightHeader =
        {
            "// Copyright (c) Microsoft. All rights reserved.",
            "// Licensed under the MIT license. See LICENSE file in the project root for full license information."
        };

        public SyntaxNode Process(SyntaxNode syntaxNode)
        {
            /*
            if (HasCopyrightHeader(syntaxNode))
                return syntaxNode;

            return AddCopyrightHeader(syntaxNode);
            */
            return syntaxNode;
        }

        private static bool HasCopyrightHeader(SyntaxNode syntaxNode)
        {
            var leadingComments = syntaxNode.GetLeadingTrivia().Where(t => t.CSharpKind() == SyntaxKind.SingleLineCommentTrivia).ToArray();
            if (leadingComments.Length < s_copyrightHeader.Length)
                return false;

            return leadingComments.Take(s_copyrightHeader.Length)
                                  .Select(t => t.ToFullString())
                                  .SequenceEqual(s_copyrightHeader);
        }

        private static SyntaxNode AddCopyrightHeader(SyntaxNode syntaxNode)
        {
            var newTrivia = GetCopyrightHeader().Concat(syntaxNode.GetLeadingTrivia());
            return syntaxNode.WithLeadingTrivia(newTrivia);
        }

        private static IEnumerable<SyntaxTrivia> GetCopyrightHeader()
        {
            foreach (var headerLine in s_copyrightHeader)
            {
                yield return SyntaxFactory.Comment(headerLine);
                yield return SyntaxFactory.CarriageReturnLineFeed;
            }
        }
    }
}