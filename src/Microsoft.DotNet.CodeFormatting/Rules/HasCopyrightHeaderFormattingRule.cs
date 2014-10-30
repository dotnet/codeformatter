// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
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
    [Export(typeof(IFormattingRule))]
    internal sealed class HasCopyrightHeaderFormattingRule : IFormattingRule
    {
        static readonly string[] CopyrightHeader =
        {
            "// Copyright (c) Microsoft Corporation. All rights reserved.",
            "// Licensed under MIT. See LICENSE in the project root for license information."
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
            if (leadingComments.Length < CopyrightHeader.Length)
                return false;

            return leadingComments.Take(CopyrightHeader.Length)
                                  .Select(t => t.ToFullString())
                                  .SequenceEqual(CopyrightHeader);
        }

        private static SyntaxNode AddCopyrightHeader(CSharpSyntaxNode syntaxNode)
        {
            var newTrivia = GetCopyrightHeader().Concat(syntaxNode.GetLeadingTrivia());
            return syntaxNode.WithLeadingTrivia(newTrivia);
        }

        private static IEnumerable<SyntaxTrivia> GetCopyrightHeader()
        {
            foreach (var headerLine in CopyrightHeader)
            {
                yield return SyntaxFactory.Comment(headerLine);
                yield return SyntaxFactory.CarriageReturnLineFeed;
            }
        }
    }
}