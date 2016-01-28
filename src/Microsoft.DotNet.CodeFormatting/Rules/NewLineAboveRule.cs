// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    /// <summary>
    /// Ensure there is a blank line above the first using and namespace in the file. 
    /// </summary>
    [SyntaxRule(NewLineAboveRule.Name, NewLineAboveRule.Description, SyntaxRuleOrder.NewLineAboveFormattingRule)]
    internal sealed class NewLineAboveRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        internal const string Name = "NewLineAbove";
        internal const string Description = "Ensure there is a new line above the first namespace and using in the file";

        public SyntaxNode Process(SyntaxNode syntaxRoot, string languageName)
        {
            syntaxRoot = ProcessUsing(syntaxRoot);
            syntaxRoot = ProcessNamespace(syntaxRoot);
            return syntaxRoot;
        }

        private SyntaxNode ProcessUsing(SyntaxNode syntaxRoot)
        {
            var firstUsing = syntaxRoot.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
            if (firstUsing == null)
            {
                return syntaxRoot;
            }

            return ProcessCore(syntaxRoot, firstUsing);
        }

        private SyntaxNode ProcessNamespace(SyntaxNode syntaxRoot)
        {
            var firstNamespace = syntaxRoot.DescendantNodesAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (firstNamespace == null)
            {
                return syntaxRoot;
            }

            var list = firstNamespace.GetLeadingTrivia();
            if (list.Count == 0)
            {
                var newLine = SyntaxUtil.GetBestNewLineTrivia(firstNamespace);
                list = list.Add(newLine);
                return syntaxRoot.ReplaceNode(firstNamespace, firstNamespace.WithLeadingTrivia(list));
            }
            else if (list.Count == 1 && list[0].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                // The namespace node is typically preceeded by a using node.  In thate case the trivia will
                // be split between the two nodes.  If the namespace already has a newline leading trivia then
                // there is at least a single blank between the nodes as the using will have a trailing new
                // line as well (in case of a single on it will be on the using).  
                return syntaxRoot;
            }
            else
            {
                return ProcessCore(syntaxRoot, firstNamespace);
            }
        }

        private SyntaxNode ProcessCore<TNode>(SyntaxNode syntaxRoot, TNode node) where TNode : SyntaxNode
        {
            // Don't attempt to format the node if is part of a conditional compilation directive
            // because it is simply not easy to format around correctly.
            if (node.GetLeadingTrivia().Any(x => x.IsConditionalDirective() || x.IsKind(SyntaxKind.DisabledTextTrivia)))
            {
                return syntaxRoot;
            }

            var newTriviaList = GetNewLeadingTrivia(node);
            if (newTriviaList == node.GetLeadingTrivia())
            {
                return syntaxRoot;
            }

            return syntaxRoot.ReplaceNode(node, node.WithLeadingTrivia(newTriviaList));
        }

        /// <summary>
        /// Get the new leading trivia list that will add the double blank line that we are looking
        /// for.
        /// </summary>
        private SyntaxTriviaList GetNewLeadingTrivia(SyntaxNode node)
        {
            var list = node.GetLeadingTrivia();
            var searchIndex = 0;
            var newLineTrivia = SyntaxUtil.GetBestNewLineTrivia(node);
            var prev = node.FindPreviousNodeInParent();

            if (prev == null)
            {
                if (node.Span.Start == 0)
                {
                    // First item in the file.  Do nothing for this case.  
                    return list;
                }
            }
            else
            {
                // If there is no new line in the trailing trivia of the previous node then need to add 
                // one to put this node on the next line. 
                if (prev.GetTrailingTrivia().Count == 0 || !prev.GetTrailingTrivia().Last().IsAnyEndOfLine())
                {
                    list = list.Insert(0, newLineTrivia);
                    searchIndex = 1;
                }
            }

            // Ensure there are blank above #pragma directives here.  This is an attempt to maintain compatibility
            // with the original design of this rule which had special spacing rules for #pragma.  No reason
            // was given for the special casing, only tests.  
            if (searchIndex < list.Count && list[0].IsKind(SyntaxKind.PragmaWarningDirectiveTrivia) && list[0].FullSpan.Start != 0)
            {
                list = list.Insert(searchIndex, newLineTrivia);
                searchIndex++;
            }

            EnsureHasBlankLineAtEnd(ref list, searchIndex, newLineTrivia);

            return list;
        }

        /// <summary>
        /// Ensure the trivia list has a blank line at the end.  Both the second to last
        /// and final line may contain spaces. 
        ///
        /// Note: This function assumes the trivia token before <param name="startIndex" />
        /// is an end of line trivia.  
        /// </summary>
        private static void EnsureHasBlankLineAtEnd(ref SyntaxTriviaList list, int startIndex, SyntaxTrivia newLineTrivia)
        {
            const int StateNone = 0;
            const int StateEol = 1;
            const int StateBlankLine = 2;

            var state = StateEol;
            var index = startIndex;
            var eolIndex = startIndex - 1;

            while (index < list.Count)
            {
                var current = list[index];
                if (current.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    index++;
                    continue;
                }

                var isStateAnyEol = (state == StateEol || state == StateBlankLine);
                if (isStateAnyEol && current.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    state = StateBlankLine;
                }
                else if (current.IsAnyEndOfLine())
                {
                    eolIndex = index;
                    state = StateEol;
                }
                else
                {
                    state = StateNone;
                }

                index++;
            }

            switch (state)
            {
                case StateNone:
                    list = list.InsertRange(list.Count, new[] { newLineTrivia, newLineTrivia });
                    break;
                case StateEol:
                    list = list.Insert(eolIndex + 1, newLineTrivia);
                    break;
                case StateBlankLine:
                    // Nothing to do. 
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }
    }
}
