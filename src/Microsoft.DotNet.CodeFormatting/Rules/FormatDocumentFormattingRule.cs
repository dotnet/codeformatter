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
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRule(FormatDocumentFormattingRule.Name, FormatDocumentFormattingRule.Description, LocalSemanticRuleOrder.IsFormattedFormattingRule)]
    internal sealed class FormatDocumentFormattingRule : ILocalSemanticFormattingRule
    {
        internal const string Name = "FormatDocument";
        internal const string Description = "Run the language specific formatter on every document";
        private readonly Options _options;

        [ImportingConstructor]
        internal FormatDocumentFormattingRule(Options options)
        {
            _options = options;
        }

        public bool SupportsLanguage(string languageName)
        {
            return
                languageName == LanguageNames.CSharp ||
                languageName == LanguageNames.VisualBasic;
        }

        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);

            if (!_options.PreprocessorConfigurations.IsDefaultOrEmpty)
            {
                var project = document.Project;
                var parseOptions = document.Project.ParseOptions;
                foreach (var configuration in _options.PreprocessorConfigurations)
                {
                    var list = new List<string>(configuration.Length + 1);
                    list.AddRange(configuration);
                    list.Add(FormattingEngineImplementation.TablePreprocessorSymbolName);

                    var newParseOptions = WithPreprocessorSymbols(parseOptions, list);
                    document = project.WithParseOptions(newParseOptions).GetDocument(document.Id);
                    document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
                }
            }

            return await document.GetSyntaxRootAsync(cancellationToken);
        }

        private static ParseOptions WithPreprocessorSymbols(ParseOptions parseOptions, List<string> symbols)
        {
            var csharpParseOptions = parseOptions as CSharpParseOptions;
            if (csharpParseOptions != null)
            {
                return csharpParseOptions.WithPreprocessorSymbols(symbols);
            }

            var basicParseOptions = parseOptions as VisualBasicParseOptions;
            if (basicParseOptions != null)
            {
                return basicParseOptions.WithPreprocessorSymbols(symbols.Select(x => new KeyValuePair<string, object>(x, true)));
            }

            throw new NotSupportedException();
        }
    }
}