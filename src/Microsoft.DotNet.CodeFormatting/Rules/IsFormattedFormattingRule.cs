// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [Export(typeof(IFormattingRule))]
    internal sealed class IsFormattedFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var newDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
            // Roslyn formatter doesn't format code in #if false as it's considered as DisabledTextTrivia. Will be removed after the bug is fixed.
            // Doing that manually here
            var syntaxRoot = await newDocument.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            var preprocessorNamesDefined = newDocument.Project.ParseOptions.PreprocessorSymbolNames;
            var preprocessorNamesToAdd = syntaxRoot.DescendantTrivia().Where(trivia => trivia.CSharpKind() == SyntaxKind.IfDirectiveTrivia)
                .SelectMany(trivia => trivia.GetStructure().DescendantNodes().OfType<IdentifierNameSyntax>())
                .Select(identifier => identifier.Identifier.Text).Distinct().Where((name) => !preprocessorNamesDefined.Contains(name));
            
            var newParseOptions = new CSharpParseOptions().WithPreprocessorSymbols(preprocessorNamesToAdd);

            var documentToProcess = newDocument.Project.WithParseOptions(newParseOptions).GetDocument(newDocument.Id);
            documentToProcess = await Formatter.FormatAsync(documentToProcess, cancellationToken: cancellationToken);

            return documentToProcess.Project.WithParseOptions(new CSharpParseOptions().WithPreprocessorSymbols(preprocessorNamesDefined)).GetDocument(documentToProcess.Id);
        }
    }
}