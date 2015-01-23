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
        private readonly IEnumerable<ISyntaxFormattingRule> _syntaxRules;
        private readonly IEnumerable<ILocalSemanticFormattingRule> _localSemanticRules;
        private readonly IEnumerable<IGlobalSemanticFormattingRule> _globalSemanticRules;
        private readonly bool _verbose;
        private readonly Stopwatch _watch = new Stopwatch();

        [ImportingConstructor]
        public FormattingEngineImplementation(
            [ImportMany] IEnumerable<IFormattingFilter> filters,
            [ImportMany] IEnumerable<Lazy<IFormattingRule, IOrderMetadata>> rules,
            [ImportMany] IEnumerable<Lazy<ISyntaxFormattingRule, IOrderMetadata>> syntaxRules,
            [ImportMany] IEnumerable<Lazy<ILocalSemanticFormattingRule, IOrderMetadata>> localSemanticRules,
            [ImportMany] IEnumerable<Lazy<IGlobalSemanticFormattingRule, IOrderMetadata>> globalSemanticRules)
        {
            _filters = filters;
            _rules = rules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
            _syntaxRules = syntaxRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
            _localSemanticRules = localSemanticRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
            _globalSemanticRules = globalSemanticRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
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

        private void StartDocument(Document document)
        {
            Console.Write("\tProcessing {0}", document.Name);
            _watch.Restart();
        }

        private void EndDocument()
        {
            _watch.Stop();
            if (_verbose && _watch.Elapsed.TotalSeconds > 1)
            {

            }
        }

        private Task<Solution> FormatDocumentsSyntaxPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            Console.WriteLine("Syntax Pass");

            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);

                Console.Write("\tProcessing {0}", document.Name);

                watch.Restart();
                var newRoot = await documentFunc(document);
                watch.Stop();

                if (_verbose && watch.Elapsed.TotalSeconds > 1)
                {
                    Console.Write(" {0} seconds", watch.Elapsed.TotalSeconds);
                }
                Console.WriteLine();

                if (newRoot != null)
                {
                    currentSolution = currentSolution.WithDocumentSyntaxRoot(documentId, newRoot);
                }
            }

            return currentSolution;
        }

        private async Task<Solution> FormatDocumentsLocalSemanticPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            Console.WriteLine("Local Semantic Pass");
            Func<Document, Task<SyntaxTree> documentFunc = async(documentFunc) =>
                {
                    var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                    if (syntaxRoot == null)
                    {
                        return null;
                    }

                    return FormatLocalSemantic(document, syntaxRoot);
                };

            return FormatDocumentsCore(originalSolution, documentIds, documentFunc, cancellationToken);
        }

        private async Task<Solution> FormatDocumentsCore(
            Solution originalSolution, 
            IReadOnlyList<DocumentId> documentIds, 
            Func<Document, Task<SyntaxTree> documentFunc,
            CancellationToken cancellationToken)
        {
        }

        private Task<SyntaxTree> FormatSyntaxTree(Document document, SyntaxNode syntaxRoot)
        {
            foreach (var syntaxRule in _syntaxRules)
            {
                syntaxRoot = syntaxRule.Process(syntaxRoot);
            }

            return Task.FromResult(root);
        }

        private async Task<SyntaxTree> FormatLocalSemantic(Document originalDocument, SyntaxNode originalSyntaxRoot)
        {
            var currentSyntaxRoot = originalSyntaxRoot;
            foreach (var localSemanticRule in _localSemanticRules)
            {
                currentSyntaxRoot = await localSemanticRule.ProcessAsync(originalDocument, originalSyntaxRoot, currentSyntaxRoot)
            }

            return currentSyntaxRoot;
        }

        private async Task<Document> ChangeEncoding(Document document, Encoding encoding)
        {
            var text = await document.GetTextAsync();
            var newText = SourceText.From(text.ToString(), encoding);
            return document.WithText(newText);
        }
    }
}