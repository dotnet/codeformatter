// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    public static class RuleExtensions
    {
        public static IEnumerable<SyntaxTrivia> AddTwoNewLines(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed });
        }

        public static IEnumerable<SyntaxTrivia> AddNewLine(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.CarriageReturnLineFeed });
        }
        
        public static IEnumerable<SyntaxTrivia> AddWhiteSpaceTrivia(this IEnumerable<SyntaxTrivia> trivia)
        {
            return trivia.Concat(new[] { SyntaxFactory.Tab });
        }

        public async static Task<Document> GetNewDocumentWithPreprocessorSymbols(this Document document, IEnumerable<string> preprocessorNamesDefined, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            var preprocessorNamesToAdd = syntaxRoot.DescendantTrivia().Where(trivia => trivia.CSharpKind() == SyntaxKind.IfDirectiveTrivia)
                .SelectMany(trivia => trivia.GetStructure().DescendantNodes().OfType<IdentifierNameSyntax>())
                .Select(identifier => identifier.Identifier.Text).Distinct().Where((name) => !preprocessorNamesDefined.Contains(name));

            var newParseOptions = new CSharpParseOptions().WithPreprocessorSymbols(preprocessorNamesToAdd);

            return document.Project.WithParseOptions(newParseOptions).GetDocument(document.Id);
        }

        public static IEnumerable<string> DefinedProjectPreprocessorNames(this Document document)
        {
            return document.Project.ParseOptions.PreprocessorSymbolNames;
        }

        public static Document GetOriginalDocumentWithPreprocessorSymbols(this Document document, IEnumerable<string> preprocessorNamesDefined)
        {
            return document.Project.WithParseOptions(new CSharpParseOptions().WithPreprocessorSymbols(preprocessorNamesDefined)).GetDocument(document.Id);
        }

        public static void WriteConsoleError(this string msg, int lineNo, string documentName)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error (Line: " + lineNo + ", " + documentName + ") " + msg);
            Console.ResetColor();
        }
    }
}
