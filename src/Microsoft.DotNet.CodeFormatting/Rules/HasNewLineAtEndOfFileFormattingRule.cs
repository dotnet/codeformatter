// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRuleOrder(SyntaxRuleOrder.HasNewLineAtEndOfFile)]
    internal sealed class HasNewLineAtEndOfFileFormattingRule : ISyntaxFormattingRule
    {
        public SyntaxNode Process(SyntaxNode syntaxRoot)
        {
            var node = syntaxRoot.DescendantNodes().LastOrDefault();

            if (node != null)
            {
                syntaxRoot = syntaxRoot.ReplaceNode(node, RemoveNewLines(node));
            }

            var token = syntaxRoot.DescendantTokens().Single(x => x.IsKind(SyntaxKind.EndOfFileToken));

            return syntaxRoot.ReplaceToken(token, AdjustNewLines(token));
        }

        private static SyntaxNode RemoveNewLines(SyntaxNode node)
        {
            var newTrivia = Enumerable.Empty<SyntaxTrivia>();

            if (node.HasTrailingTrivia)
            {
                newTrivia = node.GetTrailingTrivia().Where(x => !x.IsKind(SyntaxKind.EndOfLineTrivia));
            }

            return node.WithTrailingTrivia(newTrivia);
        }

        private static SyntaxToken AdjustNewLines(SyntaxToken token)
        {
            var newTrivia = Enumerable.Empty<SyntaxTrivia>();

            if (token.HasLeadingTrivia)
            {
                newTrivia = token.LeadingTrivia.Where(x => !x.IsKind(SyntaxKind.EndOfLineTrivia));
            }

            return token.WithLeadingTrivia(newTrivia.AddNewLine());
        }
    }
}