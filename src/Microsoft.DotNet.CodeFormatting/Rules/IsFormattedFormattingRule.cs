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
    [LocalSemanticRuleOrder(LocalSemanticRuleOrder.IsFormattedFormattingRule)]
    internal sealed class IsFormattedFormattingRule : ILocalSemanticFormattingRule
    {
        private readonly Options _options;

        [ImportingConstructor]
        internal IsFormattedFormattingRule(Options options)
        {
            _options = options;
        }

        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);

            if (!_options.PreprocessorConfigurations.IsDefaultOrEmpty)
            {
                var project = document.Project;
                var parseOptions = (CSharpParseOptions)document.Project.ParseOptions;
                foreach (var configuration in _options.PreprocessorConfigurations)
                {
                    var newParseOptions = parseOptions.WithPreprocessorSymbols(configuration);
                    document = project.WithParseOptions(newParseOptions).GetDocument(document.Id);
                    document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
                }
            }

            return await document.GetSyntaxRootAsync(cancellationToken);
        }
    }
}