using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    /// <summary>
    /// Ensure a blank line is never followed by another blank line.
    /// </summary>
    [SyntaxRule(Name, Description, SyntaxRuleOrder.NewLineAboveFormattingRule)]
    internal sealed class SingleNewLineRule : ISyntaxFormattingRule
    {
        private const string Name = "SingleNewLine";
        private const string Description = "Ensure a blank line is never followed by another blank line.";

        public bool SupportsLanguage(string languageName)
        {
            return languageName == LanguageNames.CSharp;
        }

        public SyntaxNode Process(SyntaxNode syntaxNode, string languageName)
        {
            var tokensToReplace = syntaxNode.DescendantTokens().Where((token) =>
            {
                if (token.HasLeadingTrivia)
                {
                    return HasConsecutiveNewLines(token.LeadingTrivia);
                }

                return false;
            });

            return syntaxNode.ReplaceTokens(tokensToReplace, (_, y) => FixNewLines(y));
        }

        private static bool HasConsecutiveNewLines(IReadOnlyList<SyntaxTrivia> list)
        {
            for (int index = 0; index < list.Count;)
            {
                switch (GetNewLineKind(list, index))
                {
                    case NewLineKind.NewLine:
                        if (IsSimpleNewLine(list, index + 1))
                        {
                            return true;
                        }
                        index++;
                        break;
                    case NewLineKind.WhitespaceAndNewLine:
                        if (IsSimpleNewLine(list, index + 2))
                        {
                            return true;
                        }
                        index += 2;
                        break;
                    default:
                        while( list[index].Kind() != SyntaxKind.EndOfLineTrivia) { index++; }
                        index++;
                        break;
                }
            }

            return false;
        }

        private SyntaxToken FixNewLines(SyntaxToken token)
        {
            // Remove all of the new lines at the top
            var triviaList = token.LeadingTrivia;
            var list = new List<SyntaxTrivia>(triviaList.Count);

            for (var index = 0; index < triviaList.Count;)
            {
                var newLineKind = GetNewLineKind(triviaList, index);

                if (newLineKind == NewLineKind.NewLine)
                {
                    // peek next index
                    if (index + 1 == triviaList.Count || !IsSimpleNewLine(triviaList, index + 1))
                    {
                        list.Add(triviaList[index]);
                    }
                    index++;
                }
                else if (newLineKind == NewLineKind.WhitespaceAndNewLine)
                {
                    // peek next index
                    if (index + 1 == triviaList.Count || !IsSimpleNewLine(triviaList, index + 2))
                    {
                        // only add the new line, ignore the whitespace
                        list.Add(triviaList[index + 1]);
                    }
                    index += 2;
                }
                else
                {
                    while (triviaList[index].Kind() != SyntaxKind.EndOfLineTrivia) {
                        list.Add(triviaList[index]);
                        index++;
                    }
                    list.Add(triviaList[index]);
                    index++;
                }
            }

            var newTriviaList = SyntaxFactory.TriviaList(list);
            return token.WithLeadingTrivia(newTriviaList);
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
                    return NewLineKind.None;
            }
        }


        // change this into 'collect relevant trivia'
        private static int MovePastSimpleNewLines(IReadOnlyList<SyntaxTrivia> list, int index)
        {
            var inNewLines = true;

            int previousIndex = -1;
            while (index < list.Count && inNewLines)
            {
                switch (GetNewLineKind(list, index))
                {
                    case NewLineKind.WhitespaceAndNewLine:
                        previousIndex = index + 1;
                        index += 2;
                        break;
                    case NewLineKind.NewLine:
                        previousIndex = index;
                        index++;
                        break;
                    default:
                        return previousIndex;
                }
            }

            return previousIndex;
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

        private enum NewLineKind
        {
            WhitespaceAndNewLine,
            NewLine,
            Directive,
            None,
        }
    }
}
