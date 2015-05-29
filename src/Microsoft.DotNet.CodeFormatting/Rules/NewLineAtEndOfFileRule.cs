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
    }
}
