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
    [RuleOrder(8)]
    internal sealed class HasNoNewLineAfterOpenBraceFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;

            var openBraceTokens = syntaxRoot.DescendantTokens().Where((token) => token.CSharpKind() == SyntaxKind.OpenBraceToken);
            Func<SyntaxToken, SyntaxToken, SyntaxToken> replacementForTokens = (token, dummy) =>
            {
                int elementsToRemove = 1;
                if (token.LeadingTrivia.Count > 1)
                {
                    while (elementsToRemove < token.LeadingTrivia.Count &&
                            token.LeadingTrivia.ElementAt(elementsToRemove).CSharpKind() == SyntaxKind.EndOfLineTrivia)
                        elementsToRemove++;
                }

                var newToken = token.WithLeadingTrivia(token.LeadingTrivia.Skip(elementsToRemove));
                return newToken;
            };

            var tokensToReplace = openBraceTokens.Where((token) =>
            {
                var nextToken = token.GetNextToken();
                return (nextToken.HasLeadingTrivia && nextToken.LeadingTrivia.First().CSharpKind() == SyntaxKind.EndOfLineTrivia);
            }).Select((token) => token.GetNextToken());

            return document.WithSyntaxRoot(syntaxRoot.ReplaceTokens(tokensToReplace, replacementForTokens));
        }
    }
}
