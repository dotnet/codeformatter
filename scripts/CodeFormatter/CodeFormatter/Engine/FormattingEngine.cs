using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace CodeFormatter.Engine
{
    [Export(typeof(IFormattingEngine))]
    internal sealed class FormattingEngine : IFormattingEngine
    {
        private readonly IEnumerable<IFormattingFilter> _filters;
        private readonly IEnumerable<IFormattingRule> _rules;

        [ImportingConstructor]
        public FormattingEngine([ImportMany] IEnumerable<IFormattingFilter> filters,
                                [ImportMany] IEnumerable<Lazy<IFormattingRule, IOrderMetadata>> rules)
        {
            _filters = filters;
            _rules = rules.OrderBy(r => r.Metadata.Order).Select(r => r.Value).ToArray();
        }

        public async Task RunAsync(CancellationToken cancellationToken, Workspace workspace)
        {
            var solution = workspace.CurrentSolution;
            var documents = solution.Projects.SelectMany(p => p.Documents);

            foreach (var document in documents)
            {
                var shouldBeProcessed = await ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    continue;

                var newDocument = await RewriteDocumentAsync(cancellationToken, document);

                await SaveDocumentAsync(newDocument, cancellationToken);
            }
        }

        private static async Task SaveDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync(cancellationToken);
            using (var textWriter = new StreamWriter(document.FilePath))
                text.Write(textWriter, cancellationToken);
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

        private async Task<Document> RewriteDocumentAsync(CancellationToken cancellationToken, Document document)
        {
            var newDocument = document;

            foreach (var rule in _rules)
                newDocument = await rule.ProcessAsync(cancellationToken, newDocument);

            return newDocument;
        }
    }
}