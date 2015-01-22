// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        public Task<bool> FormatSolutionAsync(Solution solution, CancellationToken cancellationToken)
        {
            var documentIds = solution.Projects.SelectMany(x => x.DocumentIds).ToList();
            return FormatAsync(solution.Workspace, documentIds, cancellationToken);
        }

        public Task<bool> FormatProjectAsync(Project project, CancellationToken cancellationToken)
        {
            return FormatAsync(project.Solution.Workspace, project.DocumentIds, cancellationToken);
        }

        private async Task<bool> FormatAsync(Workspace workspace, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            var solution = workspace.CurrentSolution;
            var hasChanges = false;
            var longRuleList = new List<Tuple<string, TimeSpan>>();

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);
                var shouldBeProcessed = await ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                {
                    continue;
                }

                longRuleList.Clear();
                var watch = new Stopwatch();
                watch.Start();
                Console.Write("Processing document: " + document.Name);
                var newDocument = await RewriteDocumentAsync(document, longRuleList, cancellationToken);
                hasChanges |= newDocument != document;
                watch.Stop();
                Console.WriteLine(" {0} seconds", watch.Elapsed.TotalSeconds);
                foreach (var tuple in longRuleList)
                {
                    Console.WriteLine("\t{0} {1} seconds", tuple.Item1, tuple.Item2.TotalSeconds);
                }

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

        private async Task<Document> RewriteDocumentAsync(Document document, List<Tuple<string, TimeSpan>> longRuleList, CancellationToken cancellationToken)
        {
            var docText = await document.GetTextAsync();
            var originalEncoding = docText.Encoding;
            var watch = new Stopwatch();
            foreach (var rule in _rules)
            {
                watch.Start();
                document = await rule.ProcessAsync(document, cancellationToken);
                watch.Stop();
                var timeSpan = watch.Elapsed;
                if (timeSpan.TotalSeconds > 1.0)
                {
                    longRuleList.Add(Tuple.Create(rule.GetType().Name, timeSpan));
                }

                watch.Reset();
            }

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