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
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(6)]
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
                    newTrivia = GetLeadingTriviaWithEndNewLines(trivia);
                }
                else if (trivia.Last().HasStructure)
                {
                    newTrivia = GetLeadingTriviaWithEndStructure(trivia);
                }
                else
                {
                    // Add two new lines, previous element is a comment
                    newTrivia = trivia.AddTwoNewLines();
                }
            }
            else
            {
                // Add a new line, previous element is a node
                newTrivia = newTrivia.AddNewLine();
            }

            return document.WithSyntaxRoot(syntaxRoot.ReplaceNode(firstNamespace, firstNamespace.WithLeadingTrivia(newTrivia)));
        }

        private IEnumerable<SyntaxTrivia> GetLeadingTriviaWithEndNewLines(IEnumerable<SyntaxTrivia> trivia)
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
                // Add a new line (previous element is a node)
                return trivia.Take(index + 1).AddNewLine();
            }

            if (index >= 0 && trivia.ElementAt(index).HasStructure)
            {
                // Insert new lines before the structured trivia
                return GetLeadingTriviaWithEndStructure(trivia.Take(index + 1));
            }

            // Add two new lines after the last comment
            return trivia.Take(index + 1).AddTwoNewLines();
        }

        private IEnumerable<SyntaxTrivia> GetLeadingTriviaWithEndStructure(IEnumerable<SyntaxTrivia> trivia)
        {
            int index = trivia.Count() - 1;
            while (index >= 0 &&
                (trivia.ElementAt(index).HasStructure ||
                trivia.ElementAt(index).CSharpKind() == SyntaxKind.DisabledTextTrivia))
                index--;

            if (index < 0)
            {
                // Add a new line (previous element is a node) before the structure trivia list.
                return trivia.Take(0).AddNewLine().Concat(trivia);
            }

            // Add two new lines (previous element is a comment) before the structure trivia list.
            if (SyntaxKind.EndOfLineTrivia != trivia.ElementAt(index).CSharpKind())
                return trivia.Take(index + 1).AddTwoNewLines().Concat(trivia.Skip(index + 1));

            // Insert one new line before the structured trivia, previous element is new line
            if (index != 0 && SyntaxKind.EndOfLineTrivia != trivia.ElementAt(index - 1).CSharpKind())
                return trivia.Take(index + 1).AddNewLine().Concat(trivia.Skip(index + 1));

            // Already has the right format
            return trivia;
        }
    }
}
