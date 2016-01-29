// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRule(BraceNewLineRule.Name, BraceNewLineRule.Description, SyntaxRuleOrder.BraceNewLineRule)]
    internal sealed class BraceNewLineRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        internal const string Name = "BraceNewLine";
        internal const string Description = "Ensure all braces occur on a new line";

        private enum NewLineKind
        {
            WhitespaceAndNewLine,
            NewLine,
            Directive,
            None,
        }

        public SyntaxNode Process(SyntaxNode syntaxNode, string languageName)
        {
            syntaxNode = FixOpenBraces(syntaxNode);
            syntaxNode = FixCloseBraces(syntaxNode);
            return syntaxNode;
        }

        /// <summary>
        /// Fix the new lines around open brace tokens.  An open brace should be followed by content, not a blank
        /// line.  Remove those here.  
        /// </summary>
        private static SyntaxNode FixOpenBraces(SyntaxNode syntaxNode)
        {
            // Look for the open brace tokens that are followed by empty blank lines.  The new lines will
            // be attached to the next nodes, not the open brace token.
            var tokensToReplace = syntaxNode.DescendantTokens().Where((token) =>
                {
                    var nextToken = token.GetNextToken();
                    if (token.Kind() != SyntaxKind.OpenBraceToken || !nextToken.HasLeadingTrivia)
                    {
                        return false;
                    }

                    return IsSimpleNewLine(nextToken.LeadingTrivia, 0);
                }).Select(x => x.GetNextToken());

            return syntaxNode.ReplaceTokens(tokensToReplace, (_, y) => FixOpenBraceNextToken(y));
        }

        /// <summary>
        /// Remove the extra new lines on the token which immediately follows an open brace.
        /// </summary>
        private static SyntaxToken FixOpenBraceNextToken(SyntaxToken token)
        {
            if (!token.HasLeadingTrivia)
            {
                return token;
            }

            if (token.Kind() == SyntaxKind.CloseBraceToken &&
                token.LeadingTrivia.All(x => x.IsKind(SyntaxKind.WhitespaceTrivia) || x.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                // This is an open / close brace combo with no content inbetween.  Just return the 
                // close brace and let the formatter handle the white space issues.  If there was a new line 
                // between the two it will be attached to the open brace and hence maintained. 
                return token.WithLeadingTrivia(SyntaxTriviaList.Empty);
            }

            // Remove all of the new lines at the top
            var triviaList = token.LeadingTrivia;
            var list = new List<SyntaxTrivia>(triviaList.Count);
            var index = MovePastSimpleNewLines(triviaList, 0);

            while (index < triviaList.Count)
            {
                list.Add(triviaList[index]);
                index++;
            }

            var newTriviaList = SyntaxFactory.TriviaList(list);
            return token.WithLeadingTrivia(newTriviaList);
        }

        /// <summary>
        /// Close braces should never have a newline between the last line of content and the 
        /// closing brace.  Also want to remove consecutive new lines before any comments or
        /// #pragma that preceeds the close brace.
        /// </summary>
        private static SyntaxNode FixCloseBraces(SyntaxNode syntaxNode)
        {
            var tokensToReplace = syntaxNode.DescendantTokens().Where((token) =>
                {
                    return
                        token.Kind() == SyntaxKind.CloseBraceToken &&
                        token.HasLeadingTrivia &&
                        (IsSimpleNewLine(token.LeadingTrivia, 0) || IsSimpleNewLine(token.LeadingTrivia, token.LeadingTrivia.Count - 1));
                });

            return syntaxNode.ReplaceTokens(tokensToReplace, (_, y) => FixCloseBraceLeadingTrivia(y));
        }

        private static SyntaxToken FixCloseBraceLeadingTrivia(SyntaxToken token)
        {
            if (!token.HasLeadingTrivia)
            {
                return token;
            }

            var triviaList = token.LeadingTrivia;
            if (triviaList.All(x => x.IsKind(SyntaxKind.WhitespaceTrivia) || x.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                // Simplest case.  It's all new lines and white space.  
                if (EndsWithSimpleNewLine(token.GetPreviousToken().TrailingTrivia))
                {
                    triviaList = SyntaxTriviaList.Empty;
                }
                else
                {
                    // new line and we are done. 
                    triviaList = SyntaxFactory.TriviaList(SyntaxUtil.GetBestNewLineTrivia(token));
                }
            }
            else
            {
                triviaList = RemoveNewLinesFromTop(triviaList);
                triviaList = RemoveNewLinesFromBottom(triviaList);
            }

            return token.WithLeadingTrivia(triviaList);
        }

        /// <summary>
        /// Don't allow consecutive newlines at the top of the comments / pragma before a close
        /// brace token.
        /// </summary>
        private static SyntaxTriviaList RemoveNewLinesFromTop(SyntaxTriviaList triviaList)
        {
            // This rule only needs to run if there is a new line at the top.
            if (!IsSimpleNewLine(triviaList, 0))
            {
                return triviaList;
            }

            var list = new List<SyntaxTrivia>(triviaList.Count);
            list.Add(SyntaxUtil.GetBestNewLineTrivia(triviaList));

            var index = MovePastSimpleNewLines(triviaList, 0);
            while (index < triviaList.Count)
            {
                list.Add(triviaList[index]);
                index++;
            }

            return SyntaxFactory.TriviaList(list);
        }

        /// <summary>
        /// Remove all extra new lines from the begining of a close brace token.  This is only called in
        /// the case at least one new line is present hence we don't have to worry about the single line
        /// case.
        /// </summary>
        private static SyntaxTriviaList RemoveNewLinesFromBottom(SyntaxTriviaList triviaList)
        {
            var index = triviaList.Count - 1;
            var searching = true;
            while (index >= 0 && searching)
            {
                var current = triviaList[index];
                switch (current.Kind())
                {
                    case SyntaxKind.WhitespaceTrivia:
                    case SyntaxKind.EndOfLineTrivia:
                        index--;
                        break;
                    default:
                        searching = false;
                        break;
                }
            }

            // Nothing to adjust, the removal of new lines from the top of the list will handle all of the
            // important cases.
            if (index < 0)
            {
                return triviaList;
            }

            var list = new List<SyntaxTrivia>(triviaList.Count);
            for (int i = 0; i <= index; i++)
            {
                list.Add(triviaList[i]);
            }

            // A directive has an implicit new line after it.
            if (!list[index].IsDirective)
            {
                list.Add(SyntaxUtil.GetBestNewLineTrivia(triviaList));
            }

            if (triviaList.Last().IsKind(SyntaxKind.WhitespaceTrivia))
            {
                list.Add(triviaList.Last());
            }

            return SyntaxFactory.TriviaList(list);
        }

        private static NewLineKind GetNewLineKind(IReadOnlyList<SyntaxTrivia> list, int index)
        {
            if (index >= list.Count)
            {
                return NewLineKind.None;
            }

            switch (list[index].Kind())
            {
                case SyntaxKind.EndOfLineTrivia:
                    return NewLineKind.NewLine;
                case SyntaxKind.WhitespaceTrivia:
                    if (index + 1 < list.Count && list[index + 1].Kind() == SyntaxKind.EndOfLineTrivia)
                    {
                        return NewLineKind.WhitespaceAndNewLine;
                    }

                    return NewLineKind.None;
                default:
                    if (list[index].IsDirective)
                    {
                        return NewLineKind.Directive;
                    }

                    return NewLineKind.None;
            }
        }

        private static bool IsSimpleNewLine(IReadOnlyList<SyntaxTrivia> list, int index)
        {
            switch (GetNewLineKind(list, index))
            {
                case NewLineKind.NewLine:
                case NewLineKind.WhitespaceAndNewLine:
                    return true;
                default:
                    return false;
            }
        }

        private static bool EndsWithSimpleNewLine(IReadOnlyList<SyntaxTrivia> list)
        {
            if (list.Count == 0)
            {
                return false;
            }

            if (list.Last().IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }

            if (list.Count > 1 &&
                list[list.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia) &&
                list[list.Count - 2].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }

            return false;
        }

        private static int MovePastSimpleNewLines(IReadOnlyList<SyntaxTrivia> list, int index)
        {
            var inNewLines = true;
            while (index < list.Count && inNewLines)
            {
                switch (GetNewLineKind(list, index))
                {
                    case NewLineKind.WhitespaceAndNewLine:
                        index += 2;
                        break;
                    case NewLineKind.NewLine:
                        index++;
                        break;
                    default:
                        inNewLines = false;
                        break;
                }
            }

            return index;
        }
    }
}
