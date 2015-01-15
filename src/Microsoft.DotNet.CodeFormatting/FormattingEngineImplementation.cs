// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
            var unformattableDocuments = new List<Document>();

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);

                // Make sure we only format files which actually exist on disk. If we don't do this,
                // workspace.TryApplyChanges() will create empty files for us, which we don't want.
                // Also make sure we don't try to write files which are read-only as this will cause
                // workspace.TryApplyChanges() to fail with an UnauthorizedAccessException.
                var fileInfo = new FileInfo(document.FilePath);
                if (!fileInfo.Exists || fileInfo.IsReadOnly)
                {
                    string skipReason = !fileInfo.Exists ? "does not exist" : "is read-only";
                    Console.WriteLine("Warning: skipping '{0}' because it {1}", document.FilePath, skipReason);
                    unformattableDocuments.Add(document);
                    solution = solution.RemoveDocument(id);
                    continue;
                }

                var shouldBeProcessed = await ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    continue;

                Console.WriteLine("Processing document: " + document.Name);
                var newDocument = await RewriteDocumentAsync(document, cancellationToken);
                hasChanges |= newDocument != document;

                solution = newDocument.Project.Solution;
            }

            // Add the documents which were deemed to be unformattable back to the solution so that
            // we don't end up modifying project files.
            foreach (var document in unformattableDocuments)
            {
                solution = solution.AddDocument(
                    document.Id,
                    document.Name,
                    await document.GetTextAsync(cancellationToken),
                    document.Folders,
                    document.FilePath);
            }

            if (workspace.TryApplyChanges(solution))
            {
                Console.WriteLine("Solution changes committed");
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