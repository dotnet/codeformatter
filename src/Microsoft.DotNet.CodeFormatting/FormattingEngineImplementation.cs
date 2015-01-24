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
        private readonly Options _options;
        private readonly IEnumerable<IFormattingFilter> _filters;
        private readonly IEnumerable<ISyntaxFormattingRule> _syntaxRules;
        private readonly IEnumerable<ILocalSemanticFormattingRule> _localSemanticRules;
        private readonly IEnumerable<IGlobalSemanticFormattingRule> _globalSemanticRules;
        private readonly Stopwatch _watch = new Stopwatch();
        private bool _verbose;

        public ImmutableArray<string> CopyrightHeader
        {
            get { return _options.CopyrightHeader; }
            set { _options.CopyrightHeader = value; }
        }

        public IFormatLogger FormatLogger
        {
            get { return _options.FormatLogger; }
            set { _options.FormatLogger = value; }
        }

        public bool Verbose
        {
            get { return _verbose; }
            set { _verbose = value; }
        }

        [ImportingConstructor]
        internal FormattingEngineImplementation(
            Options options,
            [ImportMany] IEnumerable<IFormattingFilter> filters,
            [ImportMany] IEnumerable<Lazy<ISyntaxFormattingRule, IOrderMetadata>> syntaxRules,
            [ImportMany] IEnumerable<Lazy<ILocalSemanticFormattingRule, IOrderMetadata>> localSemanticRules,
            [ImportMany] IEnumerable<Lazy<IGlobalSemanticFormattingRule, IOrderMetadata>> globalSemanticRules)
        {
            _options = options;
            _filters = filters;
            _syntaxRules = syntaxRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value).ToList();
            _localSemanticRules = localSemanticRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value).ToList();
            _globalSemanticRules = globalSemanticRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value).ToList();
        }

        public Task FormatSolutionAsync(Solution solution, CancellationToken cancellationToken)
        {
            var documentIds = solution.Projects.SelectMany(x => x.DocumentIds).ToList();
            return FormatAsync(solution.Workspace, documentIds, cancellationToken);
        }

        public Task FormatProjectAsync(Project project, CancellationToken cancellationToken)
        {
            return FormatAsync(project.Solution.Workspace, project.DocumentIds, cancellationToken);
        }

        private async Task FormatAsync(Workspace workspace, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            var watch = new Stopwatch();
            watch.Start();

            var originalSolution = workspace.CurrentSolution;
            var solution = await FormatCoreAsync(originalSolution, documentIds, cancellationToken);

            watch.Stop();

            await SaveChanges(solution, originalSolution, cancellationToken);
            FormatLogger.WriteLine("Total time {0}", watch.Elapsed);
        }

        internal async Task<Solution> FormatCoreAsync(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            var solution = originalSolution;
            solution = await RunSyntaxPass(solution, documentIds, cancellationToken);
            solution = await RunLocalSemanticPass(solution, documentIds, cancellationToken);
            solution = await RunGlobalSemanticPass(solution, documentIds, cancellationToken);
            return solution;
        }

        private async Task SaveChanges(Solution solution, Solution originalSolution, CancellationToken cancellationToken)
        {
            foreach (var projectChange in solution.GetChanges(originalSolution).GetProjectChanges())
            {
                foreach (var documentId in projectChange.GetChangedDocuments())
                {
                    var document = solution.GetDocument(documentId);
                    var sourceText = await document.GetTextAsync(cancellationToken);
                    using (var file = File.Open(document.FilePath, FileMode.Truncate, FileAccess.Write))
                    {
                        // The encoding of the file can change during the rewrite.  Make sure to save with the
                        // original encoding
                        var originalDocument = originalSolution.GetDocument(documentId);
                        var originalSourceText = await originalDocument.GetTextAsync(cancellationToken);

                        using (var writer = new StreamWriter(file, originalSourceText.Encoding))
                        {
                            sourceText.Write(writer, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task<bool> ShouldBeProcessedAsync(Document document)
        {
            if (document.FilePath != null)
            {
                var fileInfo = new FileInfo(document.FilePath);
                if (!fileInfo.Exists || fileInfo.IsReadOnly)
                {
                    FormatLogger.WriteLine("warning: skipping document '{0}' because it {1}.",
                        document.FilePath,
                        fileInfo.IsReadOnly ? "is read-only" : "does not exist");
                    return false;
                }
            }

            foreach (var filter in _filters)
            {
                var shouldBeProcessed = await filter.ShouldBeProcessedAsync(document);
                if (!shouldBeProcessed)
                    return false;
            }

            return true;
        }

        private async Task<SyntaxNode> GetSyntaxRootAndFilter(Document document, CancellationToken cancellationToken)
        {
            if (!await ShouldBeProcessedAsync(document))
            {
                return null;
            }

            return await document.GetSyntaxRootAsync(cancellationToken);
        }

        private void StartDocument()
        {
            _watch.Restart();
        }

        private void EndDocument(Document document)
        {
            _watch.Stop();
            FormatLogger.WriteLine("    {0} {1} seconds", document.Name, _watch.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// Semantics is not involved in this pass at all.  It is just a straight modification of the 
        /// parse tree so there are no issues about ensuring the version of <see cref="SemanticModel"/> and
        /// the <see cref="SyntaxNode"/> line up.  Hence we do this by iteraning every <see cref="Document"/> 
        /// and processing all rules against them at once 
        /// </summary>
        private async Task<Solution> RunSyntaxPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("Syntax Pass");

            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument();
                var newRoot = RunSyntaxPass(syntaxRoot);
                EndDocument(document);

                if (newRoot != syntaxRoot)
                {
                    currentSolution = currentSolution.WithDocumentSyntaxRoot(document.Id, newRoot);
                }
            }

            return currentSolution;
        }

        private SyntaxNode RunSyntaxPass(SyntaxNode root)
        {
            foreach (var rule in _syntaxRules)
            {
                root = rule.Process(root);
            }

            return root;
        }

        private async Task<Solution> RunLocalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("Local Semantic Pass");
            foreach (var localSemanticRule in _localSemanticRules)
            {
                solution = await RunLocalSemanticPass(solution, documentIds, localSemanticRule, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> RunLocalSemanticPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, ILocalSemanticFormattingRule localSemanticRule, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("  {0}", localSemanticRule.GetType().Name);
            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument();
                var newRoot = await localSemanticRule.ProcessAsync(document, syntaxRoot, cancellationToken);
                EndDocument(document);

                if (syntaxRoot != newRoot)
                {
                    currentSolution = currentSolution.WithDocumentSyntaxRoot(documentId, newRoot);
                }
            }

            return currentSolution;
        }

        private async Task<Solution> RunGlobalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("Global Semantic Pass");
            foreach (var globalSemanticRule in _globalSemanticRules)
            {
                solution = await RunGlobalSemanticPass(solution, documentIds, globalSemanticRule, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> RunGlobalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, IGlobalSemanticFormattingRule globalSemanticRule, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("  {0}", globalSemanticRule.GetType().Name);
            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument();
                solution = await globalSemanticRule.ProcessAsync(document, syntaxRoot, cancellationToken);
                EndDocument(document);
            }

            return solution;
        }
    }
}
