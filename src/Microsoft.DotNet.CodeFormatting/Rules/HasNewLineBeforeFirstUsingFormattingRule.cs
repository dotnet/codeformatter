// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    [SyntaxRuleOrder(SyntaxRuleOrder.HasNewLineBeforeFirstUsingFormattingRule)]
    internal sealed class HasNewLineBeforeFirstUsingFormattingRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        private const string FormatError = "Could not format using";

        public SyntaxNode Process(SyntaxNode syntaxRoot, string languageName)
        {
            var firstUsing = syntaxRoot.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
            if (firstUsing == null)
                return syntaxRoot;

            IEnumerable<SyntaxTrivia> newTrivia = Enumerable.Empty<SyntaxTrivia>();

            if (firstUsing.HasLeadingTrivia)
            {
                var trivia = firstUsing.GetLeadingTrivia();
                var x = trivia.FirstOrDefault(t => t.CSharpKind() == SyntaxKind.DisabledTextTrivia && t.ToFullString().Contains("using"));
                var disabledUsingSpan = trivia.FirstOrDefault(t => t.CSharpKind() == SyntaxKind.DisabledTextTrivia && t.ToFullString().Contains("using")).Span;
                if (!disabledUsingSpan.IsEmpty)
                {
                    Console.WriteLine("!!!Error with using");
                    return syntaxRoot;
                }

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

            return syntaxRoot.ReplaceNode(firstUsing, firstUsing.WithLeadingTrivia(newTrivia));
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
                // Return empty trivia, there is no previous element: start of file.
                return Enumerable.Empty<SyntaxTrivia>();
            }

            if (trivia.ElementAt(index).HasStructure)
            {
                // Insert new lines before the structured trivia
                return GetLeadingTriviaWithEndStructure(trivia.Take(index + 1));
            }

            // Add two new lines, previous element is a comment
            return trivia.Take(index + 1).AddTwoNewLines();
        }

        private IEnumerable<SyntaxTrivia> GetLeadingTriviaWithEndStructure(IEnumerable<SyntaxTrivia> trivia)
        {
            int index = trivia.Count() - 1;
            while (index >= 0 && trivia.ElementAt(index).HasStructure)
                index--;

            if (index < 0)
            {
                // There is no element before the structured trivia
                return trivia;
            }

            // Insert two new lines before the structured trivia, previous element is a comment
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
