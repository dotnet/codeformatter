// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.CodeFormatting
{
    [Export(typeof(IFormattingEngine))]
    internal sealed class FormattingEngineImplementation : IFormattingEngine
    {
        private readonly IEnumerable<IFormattingFilter> _filters;
        private readonly IEnumerable<IFormattingRule> _rules;


        [ImportingConstructor]
        public FormattingEngineImplementation([ImportMany] IEnumerable<IFormattingFilter> filters,
                                              [ImportMany] IEnumerable<Lazy<IFormattingRule, IOrderMetadata>> rules)
        {
            _filters = filters;
            _rules = rules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
        }

        public async Task<bool> RunAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            var solution = workspace.CurrentSolution;
            var documentIds = solution.Projects.SelectMany(p => p.DocumentIds);
            var hasChanges = false;

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);

                var fileInfo = new FileInfo(document.FilePath);
                if (!fileInfo.Exists || fileInfo.IsReadOnly)
                {
                    Console.WriteLine("warning: skipping document '{0}' because it {1}.",
                        document.FilePath,
                        fileInfo.IsReadOnly ? "is read-only" : "does not exist");
                    continue;
                }

                var shouldBeProcessed = await ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    continue;

                Console.WriteLine("Processing document: " + document.Name);
                var newDocument = await RewriteDocumentAsync(document, cancellationToken);

                if (newDocument != document)
                {
                    Debug.Assert(newDocument.FilePath == document.FilePath, "Formatting rules should not change a document's file path");

                    hasChanges = true;
                    var sourceText = await newDocument.GetTextAsync(cancellationToken);

                    using (var file = File.Open(newDocument.FilePath, FileMode.Truncate, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(file, sourceText.Encoding))
                        {
                            sourceText.Write(writer, cancellationToken);
                        }
                    }
                }

                solution = newDocument.Project.Solution;
            }

            return hasChanges;
        }

        private async Task<bool> ShouldBeProcessedAsync(Document document)
        {
            foreach (var filter in _filters)
            {
                var shouldBeProcessed = await filter.ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    return false;
            }

            return true;
        }

        private async Task<Document> RewriteDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            var docText = await document.GetTextAsync();
            var originalEncoding = docText.Encoding;
            foreach (var rule in _rules)
                document = await rule.ProcessAsync(document, cancellationToken);

            return await ChangeEncoding(document, originalEncoding);
        }

        private async Task<Document> ChangeEncoding(Document document, Encoding encoding)
        {
            var text = await document.GetTextAsync();
            var newText = SourceText.From(text.ToString(), encoding);
            return document.WithText(newText);
        }
    }
}