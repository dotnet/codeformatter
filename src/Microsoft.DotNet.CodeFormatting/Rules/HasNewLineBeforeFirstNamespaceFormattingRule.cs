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
    [ExportMetadata("Order", 6)]
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
                if (SyntaxKind.EndOfLineTrivia == trivia.Last().CSharpKind())
                {
                    newTrivia = GetCorrectedTrivia(trivia);
                }
                else if (trivia.Last().HasStructure)
                {
                    var previousTrivia = trivia.Take(trivia.Count - 1);
                    newTrivia = GetCorrectedTrivia(previousTrivia).Concat(new[] { trivia.Last() });
                }
                else
                {
                    newTrivia = trivia.AddTwoNewLines();
                }
            }
            else
            {
                newTrivia = newTrivia.AddNewLine();
            }

            return document.WithSyntaxRoot(syntaxRoot.ReplaceNode(firstNamespace, firstNamespace.WithLeadingTrivia(newTrivia)));
        }

        private IEnumerable<SyntaxTrivia> GetCorrectedTrivia(IEnumerable<SyntaxTrivia> trivia)
        {
            int index = trivia.Count() - 2;
            while (index >= 0)
            {
                if (SyntaxKind.EndOfLineTrivia != trivia.ElementAt(index).CSharpKind())
                    break;
                index--;
            }

            if (index < 0)
            {
                return trivia.Take(index + 1).AddNewLine();
            }

            if (index >= 0 && trivia.ElementAt(index).HasStructure)
            {
                return trivia.Take(index + 1);
            }

            return trivia.Take(index + 1).AddTwoNewLines();
        }
    }
}
