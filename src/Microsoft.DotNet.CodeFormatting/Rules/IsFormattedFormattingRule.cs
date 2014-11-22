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
    [RuleOrder(11)]
    internal sealed class IsFormattedFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var newDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
            // TODO Bug 1076609: Roslyn formatter doesn't format code in #if false as it's considered as DisabledTextTrivia. Will be removed after the bug is fixed.
            // Doing that manually here
            var preprocessorNames = document.DefinedProjectPreprocessorNames();
            newDocument = await newDocument.GetNewDocumentWithPreprocessorSymbols(preprocessorNames, cancellationToken);
            newDocument = await Formatter.FormatAsync(newDocument, cancellationToken: cancellationToken);

            return newDocument.GetOriginalDocumentWithPreprocessorSymbols(preprocessorNames);
        }
    }
}