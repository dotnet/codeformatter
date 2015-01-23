// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    [RuleOrder(RuleOrder.IsFormattedFormattingRule)]
    internal sealed class IsFormattedFormattingRule : ILocalSemanticFormattingRule
    {
        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var newDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
            // TODO Bug 1076609: Roslyn formatter doesn't format code in #if false as it's considered as DisabledTextTrivia. Will be removed after the bug is fixed.
            // Doing that manually here
            var preprocessorNames = document.DefinedProjectPreprocessorNames();
            newDocument = await newDocument.GetNewDocumentWithPreprocessorSymbols(preprocessorNames, cancellationToken);
            newDocument = await Formatter.FormatAsync(newDocument, cancellationToken: cancellationToken);

            return await newDocument.GetOriginalDocumentWithPreprocessorSymbols(preprocessorNames).GetSyntaxRootAsync(cancellationToken);
        }
    }
}