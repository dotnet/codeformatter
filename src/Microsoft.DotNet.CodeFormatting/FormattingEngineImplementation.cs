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
                                              [ImportMany] IEnumerable<IFormattingRule> rules)
        {
            _filters = filters;
            _rules = rules;
        }

        public async Task<bool> RunAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            var solution = workspace.CurrentSolution;
            var documents = solution.Projects.SelectMany(p => p.Documents);
            var hasChanges = false;

            foreach (var document in documents)
            {
                var shouldBeProcessed = await ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    continue;
                
                var newDocument = await RewriteDocumentAsync(document, cancellationToken);
                hasChanges |= newDocument != document;

                await SaveDocumentAsync(newDocument, cancellationToken);
                Console.WriteLine("Processing document: " + document.Name);
            }

            return hasChanges;
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

        private async Task<Document> RewriteDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            var newDocument = document;

            // There is no good ordering for formatting rules because they might interact with each other.
            //
            // Thus, we'll run the formatting until no more changes occur. In theory, we could run into an
            // infinite loop if two formatting rules are undoing each other's change.
            // 
            // We'll ignore this for now.

            while (true)
            {
                var previousDocument = newDocument;

                foreach (var rule in _rules)
                    newDocument = await rule.ProcessAsync(newDocument, cancellationToken);

                if (IsEqual(newDocument, previousDocument))
                    break;
            }

            return newDocument;
        }

        private bool IsEqual(Document newDocument, Document previousDocument)
        {
            if (newDocument.GetTextAsync().Result.ToString() == previousDocument.GetTextAsync().Result.ToString())
                return true;
            return false;
        }
    }
}