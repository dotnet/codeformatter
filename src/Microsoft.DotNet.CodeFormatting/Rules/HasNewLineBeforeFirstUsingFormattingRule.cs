// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    // [Export(typeof(IFormattingRule))]
    // This rule negates the changes done by the IsFormatterFormattingRule, when there are #ifdef using 
    // statements. Thus the formatter will enter an infinite loop. Enable this rule as required.
    internal sealed class HasNewLineBeforeFirstUsingFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;
           
            var firstUsing = syntaxRoot.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
            IEnumerable<SyntaxTrivia> newTrivia = Enumerable.Empty<SyntaxTrivia>();

            if (firstUsing == null)
                return document;

            if (firstUsing.HasLeadingTrivia)
            {
                var trivia = firstUsing.GetLeadingTrivia();
                if (trivia.Last().CSharpKind() == SyntaxKind.EndOfLineTrivia)
                {
                    int index = trivia.Count - 2;
                    while (index >= 0)
                    {
                        if (SyntaxKind.EndOfLineTrivia != trivia.ElementAt(index).CSharpKind())
                            break;
                        index--;
                    }

                    newTrivia = trivia.Take(index + 1);
                }
                else
                {
                    newTrivia = trivia;
                }                
            }

            newTrivia = newTrivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed });

            return document.WithSyntaxRoot(syntaxRoot.ReplaceNode(firstUsing, firstUsing.WithLeadingTrivia(newTrivia)));
        }
    }
}
