using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    internal static class Extensions
    {
        internal static IEnumerable<SyntaxTrivia> AddTwoNewLines(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed });
        }

        internal static IEnumerable<SyntaxTrivia> AddNewLine(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed });
        }
    }
}
