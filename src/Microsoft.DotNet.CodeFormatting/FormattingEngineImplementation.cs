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
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting
{
    [Export(typeof(IFormattingEngine))]
    internal sealed class FormattingEngineImplementation : IFormattingEngine
    {
        /// <summary>
        /// Developers who want to opt out of the code formatter for items like unicode
        /// tables can surround them with #if !DOTNET_FORMATTER.  
        /// </summary>
        internal const string TablePreprocessorSymbolName = "DOTNET_FORMATTER";

        private readonly Options _options;
        private readonly IEnumerable<IFormattingFilter> _filters;
        private readonly IEnumerable<ISyntaxFormattingRule> _syntaxRules;
        private readonly IEnumerable<ILocalSemanticFormattingRule> _localSemanticRules;
        private readonly IEnumerable<IGlobalSemanticFormattingRule> _globalSemanticRules;
        private readonly Stopwatch _watch = new Stopwatch();
        private bool _allowTables;
        private bool _verbose;

        public ImmutableArray<string> CopyrightHeader
        {
            get { return _options.CopyrightHeader; }
            set { _options.CopyrightHeader = value; }
        }

        public ImmutableArray<string[]> PreprocessorConfigurations
        {
            get { return _options.PreprocessorConfigurations; }
            set { _options.PreprocessorConfigurations = value; }
        }

        public ImmutableArray<string> FileNames
        {
            get { return _options.FileNames; }
            set { _options.FileNames = value; }
        }

        public IFormatLogger FormatLogger
        {
            get { return _options.FormatLogger; }
            set { _options.FormatLogger = value; }
        }

        public bool AllowTables
        {
            get { return _allowTables; }
            set { _allowTables = value; }
        }

        public bool Verbose
        {
            get { return _verbose; }
            set { _verbose = value; }
        }
        
        public bool ConvertUnicodeCharacters
        {
            get { return _options.ConvertUnicodeCharacters; }
            set { _options.ConvertUnicodeCharacters = value; }
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

            if (!workspace.TryApplyChanges(solution))
            {
                FormatLogger.WriteErrorLine("Unable to save changes to disk");
            }

            FormatLogger.WriteLine("Total time {0}", watch.Elapsed);
        }

        private Solution AddTablePreprocessorSymbol(Solution solution)
        {
            var projectIds = solution.ProjectIds;
            foreach (var projectId in projectIds)
            {
                var project = solution.GetProject(projectId);
                var parseOptions = project.ParseOptions as CSharpParseOptions;
                if (parseOptions != null)
                {
                    var list = new List<string>();
                    list.AddRange(parseOptions.PreprocessorSymbolNames);
                    list.Add(TablePreprocessorSymbolName);
                    parseOptions = parseOptions.WithPreprocessorSymbols(list);
                    solution = project.WithParseOptions(parseOptions).Solution;
                }
            }

            return solution;
        }

        /// <summary>
        /// Remove the added table preprocessor symbol.  Don't want that saved into the project
        /// file as a change. 
        /// </summary>
        private Solution RemoveTablePreprocessorSymbol(Solution newSolution, Solution oldSolution)
        {
            var projectIds = newSolution.ProjectIds;
            foreach (var projectId in projectIds)
            {
                var oldProject = oldSolution.GetProject(projectId);
                var newProject = newSolution.GetProject(projectId);
                newSolution = newProject.WithParseOptions(oldProject.ParseOptions).Solution;
            }

            return newSolution;
        }

        internal async Task<Solution> FormatCoreAsync(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            var solution = originalSolution;

            if (_allowTables)
            {
                solution = AddTablePreprocessorSymbol(originalSolution);
            }

            solution = await RunSyntaxPass(solution, documentIds, cancellationToken);
            solution = await RunLocalSemanticPass(solution, documentIds, cancellationToken);
            solution = await RunGlobalSemanticPass(solution, documentIds, cancellationToken);

            if (_allowTables)
            {
                solution = RemoveTablePreprocessorSymbol(solution, originalSolution);
            }

            return solution;
        }

        private bool ShouldBeProcessed(Document document)
        {
            foreach (var filter in _filters)
            {
                var shouldBeProcessed = filter.ShouldBeProcessed(document);
                if (!shouldBeProcessed)
                    return false;
            }

            return true;
        }

        private Task<SyntaxNode> GetSyntaxRootAndFilter(Document document, CancellationToken cancellationToken)
        {
            if (!ShouldBeProcessed(document))
            {
                return Task.FromResult<SyntaxNode>(null);
            }

            return document.GetSyntaxRootAsync(cancellationToken);
        }

        private Task<SyntaxNode> GetSyntaxRootAndFilter(IFormattingRule formattingRule, Document document, CancellationToken cancellationToken)
        {
            if (!formattingRule.SupportsLanguage(document.Project.Language))
            {
                return Task.FromResult<SyntaxNode>(null);
            }

            return GetSyntaxRootAndFilter(document, cancellationToken);
        }

        private void StartDocument()
        {
            _watch.Restart();
        }

        private void EndDocument(Document document)
        {
            _watch.Stop();
            if (_verbose)
            {
                FormatLogger.WriteLine("    {0} {1} seconds", document.Name, _watch.Elapsed.TotalSeconds);
            }
        }

        /// <summary>
        /// Semantics is not involved in this pass at all.  It is just a straight modification of the 
        /// parse tree so there are no issues about ensuring the version of <see cref="SemanticModel"/> and
        /// the <see cref="SyntaxNode"/> line up.  Hence we do this by iteraning every <see cref="Document"/> 
        /// and processing all rules against them at once 
        /// </summary>
        private async Task<Solution> RunSyntaxPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("\tSyntax Pass");

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
                var newRoot = RunSyntaxPass(syntaxRoot, document.Project.Language);
                EndDocument(document);

                if (newRoot != syntaxRoot)
                {
                    currentSolution = currentSolution.WithDocumentSyntaxRoot(document.Id, newRoot);
                }
            }

            return currentSolution;
        }

        private SyntaxNode RunSyntaxPass(SyntaxNode root, string languageName)
        {
            foreach (var rule in _syntaxRules)
            {
                if (rule.SupportsLanguage(languageName))
                {
                    root = rule.Process(root, languageName);
                }
            }

            return root;
        }

        private async Task<Solution> RunLocalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("\tLocal Semantic Pass");
            foreach (var localSemanticRule in _localSemanticRules)
            {
                solution = await RunLocalSemanticPass(solution, documentIds, localSemanticRule, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> RunLocalSemanticPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, ILocalSemanticFormattingRule localSemanticRule, CancellationToken cancellationToken)
        {
            if (_verbose)
            {
                FormatLogger.WriteLine("  {0}", localSemanticRule.GetType().Name);
            }

            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(localSemanticRule, document, cancellationToken);
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
            FormatLogger.WriteLine("\tGlobal Semantic Pass");
            foreach (var globalSemanticRule in _globalSemanticRules)
            {
                solution = await RunGlobalSemanticPass(solution, documentIds, globalSemanticRule, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> RunGlobalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, IGlobalSemanticFormattingRule globalSemanticRule, CancellationToken cancellationToken)
        {
            if (_verbose)
            {
                FormatLogger.WriteLine("  {0}", globalSemanticRule.GetType().Name);
            }

            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(globalSemanticRule, document, cancellationToken);
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
