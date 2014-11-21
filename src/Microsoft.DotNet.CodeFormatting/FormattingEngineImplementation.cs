// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

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
                var shouldBeProcessed = await ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    continue;

                Console.WriteLine("Processing document: " + document.Name);
                var newDocument = await RewriteDocumentAsync(document, cancellationToken);
                hasChanges |= newDocument != document;

                solution = newDocument.Project.Solution;
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
            foreach (var rule in _rules)
                document = await rule.ProcessAsync(document, cancellationToken);

            return document;
        }
    }
}