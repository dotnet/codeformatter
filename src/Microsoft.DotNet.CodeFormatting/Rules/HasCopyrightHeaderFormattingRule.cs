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
    [RuleOrder(RuleOrder.HasCopyrightHeaderFormattingRule)]
    internal sealed class HasCopyrightHeaderFormattingRule : IFormattingRule
    {
        private static readonly string[] s_copyrightHeader =
        {
            "// Copyright (c) Microsoft. All rights reserved.",
            "// Licensed under the MIT license. See LICENSE file in the project root for full license information."
        };

        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
                return document;

            if (HasCopyrightHeader(syntaxNode))
                return document;

            var newNode = AddCopyrightHeader(syntaxNode);
            return document.WithSyntaxRoot(newNode);
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

        private static SyntaxNode AddCopyrightHeader(CSharpSyntaxNode syntaxNode)
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