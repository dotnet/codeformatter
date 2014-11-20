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
    [Export(typeof(IFormattingRule))]
    [ExportMetadata("Order", 5)]
    internal sealed class HasNewLineBeforeFirstUsingFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            // Roslyn formatter doesn't format code in #if false as it's considered as DisabledTextTrivia. Will be removed after the bug is fixed.
            // Doing that manually here
            var preprocessorNames = document.DefinedProjectPreprocessorNames();
            var newDocument = await document.GetNewDocumentWithPreprocessorSymbols(preprocessorNames, cancellationToken);
            newDocument = await FormatUsingInDocument(newDocument, cancellationToken);

            return await FormatUsingInDocument(newDocument.GetOriginalDocumentWithPreprocessorSymbols(preprocessorNames), cancellationToken);
        }

        public async Task<Document> FormatUsingInDocument(Document document, CancellationToken cancellationToken)
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
            
            return document.WithSyntaxRoot(syntaxRoot.ReplaceNode(firstUsing, firstUsing.WithLeadingTrivia(newTrivia)));
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
                return Enumerable.Empty<SyntaxTrivia>();
            }

            if (trivia.ElementAt(index).HasStructure)
            {
                return trivia.Take(index + 1);
            }
           
            return trivia.Take(index + 1).AddTwoNewLines();
        }
    }
}
