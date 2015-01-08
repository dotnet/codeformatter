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
    [RuleOrder(RuleOrder.HasNoNewLineBeforeEndBraceFormattingRule)]
    internal sealed class HasNoNewLineBeforeEndBraceFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;

            var closeBraceTokens = syntaxRoot.DescendantTokens().Where((token) => token.CSharpKind() == SyntaxKind.CloseBraceToken);
            Func<SyntaxToken, SyntaxToken, SyntaxToken> replaceTriviaInTokens = (token, dummy) =>
            {
                var newTrivia = RemoveNewLinesFromTop(token.LeadingTrivia);
                newTrivia = RemoveNewLinesFromBottom(newTrivia);
                return token.WithLeadingTrivia(newTrivia);
            };

            var tokensToReplace = closeBraceTokens.Where((token) => token.HasLeadingTrivia && (
                                    token.LeadingTrivia.First().CSharpKind() == SyntaxKind.EndOfLineTrivia ||
                                    token.LeadingTrivia.Last().CSharpKind() == SyntaxKind.EndOfLineTrivia ||
                                    token.LeadingTrivia.Last().CSharpKind() == SyntaxKind.WhitespaceTrivia));

            return document.WithSyntaxRoot(syntaxRoot.ReplaceTokens(tokensToReplace, replaceTriviaInTokens));
        }

        private static IEnumerable<SyntaxTrivia> RemoveNewLinesFromTop(IEnumerable<SyntaxTrivia> trivia)
        {
            int elementsToRemoveAtStart = 0;
            if (trivia.First().CSharpKind() == SyntaxKind.EndOfLineTrivia)
            {
                elementsToRemoveAtStart = 1;
                if (trivia.Count() > 1)
                {
                    while (elementsToRemoveAtStart < trivia.Count() &&
                            trivia.ElementAt(elementsToRemoveAtStart).CSharpKind() == SyntaxKind.EndOfLineTrivia)
                        elementsToRemoveAtStart++;
                }
            }

            return trivia.Skip(elementsToRemoveAtStart);
        }

        private static IEnumerable<SyntaxTrivia> RemoveNewLinesFromBottom(IEnumerable<SyntaxTrivia> trivia)
        {
            bool addWhitespace = false;
            bool addNewLine = false;
            var initialCount = trivia.Count();
            if (initialCount > 1 && trivia.Last().CSharpKind() == SyntaxKind.WhitespaceTrivia)
            {
                addWhitespace = true;
                trivia = trivia.Take(initialCount - 1);
            }
            else if (initialCount > 1 &&
                trivia.ElementAt(initialCount - 2).CSharpKind() != SyntaxKind.EndOfLineTrivia &&
                trivia.ElementAt(initialCount - 2).CSharpKind() != SyntaxKind.WhitespaceTrivia &&
                !trivia.ElementAt(initialCount - 2).HasStructure)
            {
                addNewLine = true;
            }

            int elementsToRemoveAtEnd = trivia.Count() - 1;

            if (trivia.Any() && trivia.Last().CSharpKind() == SyntaxKind.EndOfLineTrivia)
            {
                if (trivia.Count() > 1)
                {
                    while (elementsToRemoveAtEnd >= 0 &&
                            trivia.ElementAt(elementsToRemoveAtEnd).CSharpKind() == SyntaxKind.EndOfLineTrivia)
                        elementsToRemoveAtEnd--;
                }
            }

            var newTrivia = trivia.Take(elementsToRemoveAtEnd + 1);

            if (newTrivia.Any() && newTrivia.Last().CSharpKind().ToString().ToLower().Contains("comment"))
                addNewLine = true;

            if (addWhitespace)
            {
                if (newTrivia.Last().IsDirective)
                    return newTrivia.AddWhiteSpaceTrivia();

                return newTrivia.AddNewLine().AddWhiteSpaceTrivia();
            }

            if (addNewLine)
            {
                return newTrivia.AddNewLine();
            }

            return newTrivia;
        }
    }
}
