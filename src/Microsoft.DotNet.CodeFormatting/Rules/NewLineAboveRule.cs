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
    /// <summary>
    /// Ensure there is a blank line above the first using and namespace in the file. 
    /// </summary>
    [SyntaxRule(SyntaxRuleOrder.NewLineAboveFormattingRule)]
    internal sealed class NewLineAboveRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
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

            SyntaxTriviaList newTriviaList;
            if (!TryGetNewLeadingTrivia(node, out newTriviaList))
            {
                return syntaxRoot;
            }

            return syntaxRoot.ReplaceNode(node, node.WithLeadingTrivia(newTriviaList));
        }

        /// <summary>
        /// Get the new leading trivia list that will add the double blank line that we are looking
        /// for.
        /// </summary>
        private bool TryGetNewLeadingTrivia(SyntaxNode node, out SyntaxTriviaList newTriviaList)
        {
            var newLineTrivia = SyntaxUtil.GetBestNewLineTrivia(node);
            var list = node.GetLeadingTrivia();
            var index = list.Count - 1;

            MoveBackwardsPastWhitespace(list, ref index);
            if (index < 0 || !list[index].IsAnyEndOfLine())
            {
                // There is no newline before the using at all.  Add a double newline to 
                // get the blank we are looking for
                newTriviaList = list.InsertRange(index + 1, new[] { newLineTrivia, newLineTrivia });
                return true;
            }

            var wasDirective = list[index].IsDirective;
            index--;

            // Move past any directives that are above the token.  The newline needs to 
            // be above them.
            while (index >= 0 && list[index].IsDirective)
            {
                index--;
            }

            if (wasDirective)
            {
                // There was a directive above the using and index now points directly before 
                // that.  This token must be a new line.  
                if (index < 0 || !list[index].IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    newTriviaList = list.Insert(index + 1, newLineTrivia);
                    return true;
                }

                index--;
            }

            // In the logical line above the using.  Need to see <blank><eol> in order for the 
            // using to be correct
            var insertIndex = index + 1;
            MoveBackwardsPastWhitespace(list, ref index);
            if (index < 0 || !list[index].IsAnyEndOfLine())
            {
                // If this is the first item in the file then there is no need for a double 
                // blank line.  
                if (index >= 0 || node.FullSpan.Start != 0)
                {
                    newTriviaList = list.Insert(insertIndex, newLineTrivia);
                    return true;
                }
            }

            // The using is well formed so there is no work to be done. 
            newTriviaList = SyntaxTriviaList.Empty;
            return false;
        }

        private static void MoveBackwardsPastWhitespace(SyntaxTriviaList list, ref int index)
        {
            while (index >= 0 && list[index].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                index--;
            }
        }
    }
}
