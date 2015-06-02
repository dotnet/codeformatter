using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRule(SyntaxRuleOrder.NewLineAtEndOfFileRule)]
    internal sealed class NewLineAtEndOfFileRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        public SyntaxNode Process(SyntaxNode syntaxRoot, string languageName)
        {
            bool needsNewLine;
            var endOfFileToken = syntaxRoot.GetLastToken(true, true, true, true);
            if (!endOfFileToken.IsKind(SyntaxKind.EndOfFileToken))
            {
                throw new InvalidOperationException("Expected last token to be EndOfFileToken, was actually: " + endOfFileToken.Kind());
            }

            if (endOfFileToken.HasLeadingTrivia)
            {
                return AddNewLineToEndOfFileTokenLeadingTriviaIfNecessary(syntaxRoot, endOfFileToken);
            }

            var lastToken = syntaxRoot.GetLastToken();
            if (!lastToken.HasTrailingTrivia)
            {
                needsNewLine = true;
            }
            else
            {
                var lastTrivia = lastToken.TrailingTrivia.Last();
                if (lastTrivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    needsNewLine = false;
                }
                else
                {
                    needsNewLine = true;
                }
            }

            if (needsNewLine)
            {
                var newLine = SyntaxUtil.GetBestNewLineTriviaRecursive(lastToken.Parent);
                var newLastToken = lastToken.WithTrailingTrivia(lastToken.TrailingTrivia.Concat(new[] { newLine }));
                return syntaxRoot.ReplaceToken(lastToken, newLastToken);
            }
            return syntaxRoot;
        }

        SyntaxNode AddNewLineToEndOfFileTokenLeadingTriviaIfNecessary(SyntaxNode syntaxRoot, SyntaxToken endofFileToken)
        {
            if (endofFileToken.LeadingTrivia.Last().IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return syntaxRoot;
            }

            var newLine = SyntaxUtil.GetBestNewLineTriviaRecursive(endofFileToken.Parent);
            var newLastToken = endofFileToken.WithTrailingTrivia(endofFileToken.TrailingTrivia.Concat(new[] { newLine }));
            return syntaxRoot.ReplaceToken(endofFileToken, newLastToken);


            return syntaxRoot;
        }
    }
}
