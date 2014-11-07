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
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [Export(typeof(IFormattingRule))]
    internal sealed class HasNewLineBeforeFirstNamespaceFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;

            var firstNamespace = syntaxRoot.DescendantNodesAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            IEnumerable<SyntaxTrivia> newTrivia = Enumerable.Empty<SyntaxTrivia>();

            if (firstNamespace == null)
                return document;

            if (firstNamespace.HasLeadingTrivia)
            {
                var trivia = firstNamespace.GetLeadingTrivia();
                if (trivia.Last().CSharpKind() == SyntaxKind.EndOfLineTrivia)
                {
                    int index = trivia.Count - 2;
                    while (index >= 0)
                    {
                        if (trivia.ElementAt(index).CSharpKind() != SyntaxKind.EndOfLineTrivia)
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

            newTrivia = newTrivia.Concat(new[] { SyntaxFactory.EndOfLine("\n"), SyntaxFactory.EndOfLine("\n") });

            return document.WithSyntaxRoot(syntaxRoot.ReplaceNode(firstNamespace, firstNamespace.WithLeadingTrivia(newTrivia)));
        }
    }
}
