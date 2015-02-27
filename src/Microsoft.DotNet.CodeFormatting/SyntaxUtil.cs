using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    internal static class SyntaxUtil
    {
        /// <summary>
        /// Look at the context of the node to determine the best possible new line trivia.  It will prefer 
        /// existing new lines over creating a new one to help ensure the same new lines are preserved 
        /// throughout the file. 
        /// </summary>
        internal static SyntaxTrivia GetBestNewLineTrivia(SyntaxNode node, SyntaxTrivia? defaultNewLineTrivia = null)
        {
            SyntaxTrivia trivia;
            if (TryGetExistingNewLine(node.GetLeadingTrivia(), out trivia) ||
                TryGetExistingNewLine(node.GetTrailingTrivia(), out trivia))
            {
                return trivia;
            }

            return defaultNewLineTrivia ?? SyntaxFactory.CarriageReturnLineFeed;
        }

        internal static SyntaxTrivia GetBestNewLineTrivia(SyntaxToken token, SyntaxTrivia? defaultNewLineTrivia = null)
        {
            SyntaxTrivia trivia;
            if (TryGetExistingNewLine(token.LeadingTrivia, out trivia) ||
                TryGetExistingNewLine(token.TrailingTrivia, out trivia))
            {
                return trivia;
            }

            return defaultNewLineTrivia ?? SyntaxFactory.CarriageReturnLineFeed;
        }

        internal static SyntaxTrivia GetBestNewLineTrivia(SyntaxTriviaList list, SyntaxTrivia? defaultNewLineTrivia = null)
        {
            SyntaxTrivia trivia;
            if (TryGetExistingNewLine(list, out trivia))
            {
                return trivia;
            }

            return defaultNewLineTrivia ?? SyntaxFactory.CarriageReturnLineFeed;
        }

        private static bool TryGetExistingNewLine(SyntaxTriviaList list, out SyntaxTrivia newLineTrivia)
        {
            foreach (var trivia in list)
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    newLineTrivia =  trivia;
                    return true;
                }
            }

            newLineTrivia = default(SyntaxTrivia);
            return false;
        }

        /// <summary>
        /// Is this a trivia element which is a conditional directive? 
        /// </summary>
        internal static bool IsConditionalDirective(this SyntaxTrivia trivia)
        {
            switch (trivia.Kind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                case SyntaxKind.EndIfDirectiveTrivia:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Is this any trivia element which represents a new line 
        /// </summary>
        internal static bool IsAnyEndOfLine(this SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.EndOfLineTrivia) || trivia.IsDirective;
        }
    }
}
