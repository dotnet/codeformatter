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
        private readonly IEnumerable<ISyntaxFormattingRule> _syntaxRules;
        private readonly IEnumerable<ILocalSemanticFormattingRule> _localSemanticRules;
        private readonly IEnumerable<IGlobalSemanticFormattingRule> _globalSemanticRules;
        private readonly Stopwatch _watch = new Stopwatch();

        [ImportingConstructor]
        public FormattingEngineImplementation(
            [ImportMany] IEnumerable<IFormattingFilter> filters,
            [ImportMany] IEnumerable<Lazy<ISyntaxFormattingRule, IOrderMetadata>> syntaxRules,
            [ImportMany] IEnumerable<Lazy<ILocalSemanticFormattingRule, IOrderMetadata>> localSemanticRules,
            [ImportMany] IEnumerable<Lazy<IGlobalSemanticFormattingRule, IOrderMetadata>> globalSemanticRules)
        {
            _filters = filters;
            _syntaxRules = syntaxRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
            _localSemanticRules = localSemanticRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
            _globalSemanticRules = globalSemanticRules.OrderBy(r => r.Metadata.Order).Select(r => r.Value);
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
            var originalSolution = workspace.CurrentSolution;
            var solution = originalSolution;
            solution = await RunSyntaxPass(solution, documentIds, cancellationToken);
            solution = await RunLocalSemanticPass(solution, documentIds, cancellationToken);
            solution = await RunGlobalSemanticPass(solution, documentIds, cancellationToken);
            
            foreach (var projectChange in solution.GetChanges(originalSolution).GetProjectChanges())
            {
                foreach (var documentId in projectChange.GetChangedDocuments())
                {
                    var document = solution.GetDocument(documentId);
                    var sourceText = await document.GetTextAsync(cancellationToken);
                    using (var file = File.Open(document.FilePath, FileMode.Truncate, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(file, sourceText.Encoding))
                        {
                            sourceText.Write(writer, cancellationToken);
                        }
                    }
                }
            }
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

        private async Task<SyntaxNode> GetSyntaxRootAndFilter(Document document, CancellationToken cancellationToken)
        {
            if (!await ShouldBeProcessedAsync(document))
            {
                return null;
            }

            return await document.GetSyntaxRootAsync(cancellationToken);
        }

        private void StartDocument(Document document, int depth = 1)
        {
            for (int i = 0; i < depth; i++)
            {
                Console.Write("\t");
            }

            Console.Write("Processing {0}", document.Name);
            _watch.Restart();
        }

        private void EndDocument()
        {
            _watch.Stop();
            if (_watch.Elapsed.TotalSeconds > 1)
            {
                Console.WriteLine(" {0} seconds", _watch.Elapsed.TotalSeconds);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Semantics is not involved in this pass at all.  It is just a straight modification of the 
        /// parse tree so there are no issues about ensuring the version of <see cref="SemanticModel"/> and
        /// the <see cref="SyntaxNode"/> line up.  Hence we do this by iteraning every <see cref="Document"/> 
        /// and processing all rules against them at once 
        /// </summary>
        private async Task<Solution> RunSyntaxPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            Console.WriteLine("Syntax Pass");

            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument(document);
                var newRoot = RunSyntaxPass(syntaxRoot);
                EndDocument();

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
            Console.WriteLine("Local Semantic Pass");
            foreach (var localSemanticRule in _localSemanticRules)
            {
                solution = await RunLocalSemanticPass(solution, documentIds, localSemanticRule, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> RunLocalSemanticPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, ILocalSemanticFormattingRule localSemanticRule, CancellationToken cancellationToken)
        {
            Console.WriteLine("\t{0}", localSemanticRule.GetType().Name);
            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument(document, depth: 2);
                var newRoot = await localSemanticRule.ProcessAsync(document, syntaxRoot, cancellationToken);
                EndDocument();

                if (syntaxRoot != newRoot)
                {
                    currentSolution = currentSolution.WithDocumentSyntaxRoot(documentId, newRoot);
                }
            }

            return currentSolution;
        }

        private async Task<Solution> RunGlobalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            Console.WriteLine("Global Semantic Pass");
            foreach (var globalSemanticRule in _globalSemanticRules)
            {
                solution = await RunGlobalSemanticPass(solution, documentIds, globalSemanticRule, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> RunGlobalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, IGlobalSemanticFormattingRule globalSemanticRule, CancellationToken cancellationToken)
        {
            Console.WriteLine("\t{0}", globalSemanticRule.GetType().Name);
            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument(document, depth: 2);
                solution = await globalSemanticRule.ProcessAsync(document, syntaxRoot, cancellationToken);
                EndDocument();
            }

            return solution;
        }
    }
}