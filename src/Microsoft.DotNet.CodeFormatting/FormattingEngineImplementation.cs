// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatting
{
    internal sealed partial class FormattingEngineImplementation : IFormattingEngine
    {
        /// <summary>
        /// Developers who want to opt out of the code formatter for items like unicode
        /// tables can surround them with #if !DOTNET_FORMATTER.  
        /// </summary>
        internal const string TablePreprocessorSymbolName = "DOTNET_FORMATTER";

        private readonly FormattingOptions _options;
        private readonly IEnumerable<CodeFixProvider> _fixers;
        private readonly IEnumerable<IFormattingFilter> _filters;
        private readonly IList<DiagnosticAnalyzer> _cSharpAnalyzers;
        private readonly IList<DiagnosticAnalyzer> _visualBasicharpAnalyzers;
        private readonly Dictionary<string, IEnumerable<DiagnosticAnalyzer>> _languageToAnalyzersMap;
        private readonly IEnumerable<IOptionsProvider> _optionsProviders;
        private readonly IEnumerable<ExportFactory<ISyntaxFormattingRule, SyntaxRule>> _syntaxRules;
        private readonly IEnumerable<ExportFactory<ILocalSemanticFormattingRule, LocalSemanticRule>> _localSemanticRules;
        private readonly IEnumerable<ExportFactory<IGlobalSemanticFormattingRule, GlobalSemanticRule>> _globalSemanticRules;
        private readonly Stopwatch _watch = new Stopwatch();
        private readonly Dictionary<string, bool> _ruleEnabledMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly ImmutableDictionary<string, CodeFixProvider> _diagnosticIdToFixerMap;

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

        public bool AllowTables { get; set; }

        public bool Verbose { get; set; }

        public string FormattingOptionsFilePath { get; set; }

        public ImmutableArray<IRuleMetadata> AllRules
        {
            get
            {
                var list = new List<IRuleMetadata>();
                list.AddRange(_syntaxRules.Select(x => x.Metadata));
                list.AddRange(_localSemanticRules.Select(x => x.Metadata));
                list.AddRange(_globalSemanticRules.Select(x => x.Metadata));
                return list.ToImmutableArray();
            }
        }

        public ImmutableArray<DiagnosticDescriptor> AllSupportedDiagnostics
            => _cSharpAnalyzers.Union(_visualBasicharpAnalyzers)
                    .SelectMany(a => a.SupportedDiagnostics)
                    .OrderBy(a => a.Id)
                    .ToImmutableArray();

        public FormattingEngineImplementation(
            FormattingOptions options,
            IEnumerable<IFormattingFilter> filters,
            IEnumerable<DiagnosticAnalyzer> analyzers,
            IEnumerable<CodeFixProvider> fixers,
            IEnumerable<ExportFactory<ISyntaxFormattingRule, SyntaxRule>> syntaxRules,
            IEnumerable<ExportFactory<ILocalSemanticFormattingRule, LocalSemanticRule>> localSemanticRules,
            IEnumerable<ExportFactory<IGlobalSemanticFormattingRule, GlobalSemanticRule>> globalSemanticRules,
            IEnumerable<IOptionsProvider> optionProviders)
        {
            _options = options;
            _filters = filters;
            _fixers = fixers;
            _syntaxRules = syntaxRules;
            _optionsProviders = optionProviders;
            _localSemanticRules = localSemanticRules;
            _globalSemanticRules = globalSemanticRules;

            _cSharpAnalyzers = new List<DiagnosticAnalyzer>();
            _visualBasicharpAnalyzers = new List<DiagnosticAnalyzer>();

            foreach (DiagnosticAnalyzer analyzer in analyzers)
            {
                Type type = analyzer.GetType();
                var attr = (DiagnosticAnalyzerAttribute)type.GetCustomAttributes(typeof(DiagnosticAnalyzerAttribute), false)[0];
                foreach (string language in attr.Languages)
                {
                    if (language == LanguageNames.CSharp)
                    {
                        _cSharpAnalyzers.Add(analyzer);
                    }
                    else if (language == LanguageNames.VisualBasic)
                    {
                        _visualBasicharpAnalyzers.Add(analyzer);
                    }
                    else
                    {
                        Debug.Assert(false);
                    }
                }
            }

            Debug.Assert(options.CopyrightHeader != null);

            foreach (var rule in AllRules)
            {
                _ruleEnabledMap[rule.Name] = rule.DefaultRule;
            }

            _diagnosticIdToFixerMap = CreateDiagnosticIdToFixerMap();
        }

        private IEnumerable<TRule> GetOrderedRules<TRule, TMetadata>(IEnumerable<ExportFactory<TRule, TMetadata>> rules)
            where TRule : IFormattingRule
            where TMetadata : IRuleMetadata
        {
            return rules
                .OrderBy(r => r.Metadata.Order)
                .Where(r => _ruleEnabledMap[r.Metadata.Name])
                .Select(r => r.CreateExport().Value);
        }

        private ImmutableDictionary<string, CodeFixProvider> CreateDiagnosticIdToFixerMap()
        {
            var diagnosticIdToFixerMap = ImmutableDictionary.CreateBuilder<string, CodeFixProvider>();

            foreach (var fixer in _fixers)
            {
                var supportedDiagnosticIds = fixer.FixableDiagnosticIds;

                foreach (var id in supportedDiagnosticIds)
                {
                    diagnosticIdToFixerMap.Add(id, fixer);
                }
            }

            return diagnosticIdToFixerMap.ToImmutable();
        }

        public async Task FormatSolutionAsync(Solution solution, bool useAnalyzers, CancellationToken cancellationToken)
        {
            if (useAnalyzers)
            {
                await FormatSolutionWithAnalyzersAsync(solution, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FormatSolutionWithRulesAsync(solution, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FormatProjectAsync(Project project, bool useAnalyzers, CancellationToken cancellationToken)
        {
            if (useAnalyzers)
            {
                await FormatProjectWithAnalyzersAsync(project, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FormatProjectWithRulesAsync(project, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FormatSolutionWithRulesAsync(Solution solution, CancellationToken cancellationToken)
        {
            var documentIds = solution.Projects.SelectMany(x => x.DocumentIds).ToList();
            await FormatAsync(solution.Workspace, documentIds, cancellationToken).ConfigureAwait(false);
        }

        public async Task FormatProjectWithRulesAsync(Project project, CancellationToken cancellationToken)
        {
            await FormatAsync(project.Solution.Workspace, project.DocumentIds, cancellationToken).ConfigureAwait(false);
        }

        public async Task FormatSolutionWithAnalyzersAsync(Solution solution, CancellationToken cancellationToken)
        {
            foreach (var project in solution.Projects)
            {
                await FormatProjectWithAnalyzersAsync(project, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FormatProjectWithAnalyzersAsync(Project project, CancellationToken cancellationToken)
        {
            if (!project.Documents.Any())
            {
                FormatLogger.WriteLine($"Skipping {project.Name}: no files to format.");
                return;
            }

            var watch = new Stopwatch();
            watch.Start();

            var diagnostics = await GetDiagnostics(project, cancellationToken).ConfigureAwait(false);

            var batchFixer = WellKnownFixAllProviders.BatchFixer;

            var context = new FixAllContext(
                project.Documents.First(), // TODO: Shouldn't this be the whole project?
                new UberCodeFixer(_diagnosticIdToFixerMap),
                FixAllScope.Project,
                null,
                diagnostics.Select(d => d.Id),
                new FormattingEngineDiagnosticProvider(project, diagnostics),
                cancellationToken);


            var fix = await batchFixer.GetFixAsync(context).ConfigureAwait(false);
            if (fix != null)
            {
                foreach (var operation in await fix.GetOperationsAsync(cancellationToken).ConfigureAwait(false))
                {
                    operation.Apply(project.Solution.Workspace, cancellationToken);
                }
            }

            watch.Stop();
            FormatLogger.WriteLine("Total time {0}", watch.Elapsed);
        }

        public void ToggleRuleEnabled(IRuleMetadata ruleMetaData, bool enabled)
        {
            _ruleEnabledMap[ruleMetaData.Name] = enabled;
        }

        private async Task FormatAsync(Workspace workspace, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            var watch = new Stopwatch();
            watch.Start();

            var originalSolution = workspace.CurrentSolution;
            var solution = await FormatCoreAsync(originalSolution, documentIds, cancellationToken).ConfigureAwait(false);

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

            if (AllowTables)
            {
                solution = AddTablePreprocessorSymbol(originalSolution);
            }

            solution = await RunSyntaxPass(solution, documentIds, cancellationToken).ConfigureAwait(false);
            solution = await RunLocalSemanticPass(solution, documentIds, cancellationToken).ConfigureAwait(false);
            solution = await RunGlobalSemanticPass(solution, documentIds, cancellationToken).ConfigureAwait(false);

            if (AllowTables)
            {
                solution = RemoveTablePreprocessorSymbol(solution, originalSolution);
            }

            return solution;
        }

        private async Task<ImmutableArray<Diagnostic>> GetDiagnostics(Project project, CancellationToken cancellationToken)
        {
            AnalyzerOptions analyzerOptions = null;

            if (project.Language != LanguageNames.CSharp &&
                project.Language != LanguageNames.VisualBasic)
            {
                return ImmutableArray<Diagnostic>.Empty;
            }

            if (!string.IsNullOrEmpty(FormattingOptionsFilePath))
            {
                var additionalTextFile = new AdditionalTextFile(FormattingOptionsFilePath);
                analyzerOptions = new AnalyzerOptions(new AdditionalText[] { additionalTextFile }.ToImmutableArray());
            }

            IList<DiagnosticAnalyzer> analyzers = project.Language == LanguageNames.CSharp ? _cSharpAnalyzers : _visualBasicharpAnalyzers;

            if (analyzers.Count > 0)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray(), analyzerOptions);
                return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
            }

            return ImmutableArray<Diagnostic>.Empty;
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
            if (Verbose)
            {
                FormatLogger.WriteLine("    {0} {1} seconds", document.Name, _watch.Elapsed.TotalSeconds);
            }
        }

        /// <summary>
        /// Semantics is not involved in this pass at all.  It is just a straight modification of the 
        /// parse tree so there are no issues about ensuring the version of <see cref="SemanticModel"/> and
        /// the <see cref="SyntaxNode"/> line up.  Hence we do this by iterating every <see cref="Document"/> 
        /// and processing all rules against them at once 
        /// </summary>
        private async Task<Solution> RunSyntaxPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, CancellationToken cancellationToken)
        {
            FormatLogger.WriteLine("\tSyntax Pass");

            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(document, cancellationToken).ConfigureAwait(false);
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
            foreach (var rule in GetOrderedRules(_syntaxRules))
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
            foreach (var localSemanticRule in GetOrderedRules(_localSemanticRules))
            {
                solution = await RunLocalSemanticPass(solution, documentIds, localSemanticRule, cancellationToken).ConfigureAwait(false);
            }

            return solution;
        }

        private async Task<Solution> RunLocalSemanticPass(Solution originalSolution, IReadOnlyList<DocumentId> documentIds, ILocalSemanticFormattingRule localSemanticRule, CancellationToken cancellationToken)
        {
            if (Verbose)
            {
                FormatLogger.WriteLine("  {0}", localSemanticRule.GetType().Name);
            }

            var currentSolution = originalSolution;
            foreach (var documentId in documentIds)
            {
                var document = originalSolution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(localSemanticRule, document, cancellationToken).ConfigureAwait(false);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument();
                var newRoot = await localSemanticRule.ProcessAsync(document, syntaxRoot, cancellationToken).ConfigureAwait(false);
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
            foreach (var globalSemanticRule in GetOrderedRules(_globalSemanticRules))
            {
                solution = await RunGlobalSemanticPass(solution, documentIds, globalSemanticRule, cancellationToken).ConfigureAwait(false);
            }

            return solution;
        }

        private async Task<Solution> RunGlobalSemanticPass(Solution solution, IReadOnlyList<DocumentId> documentIds, IGlobalSemanticFormattingRule globalSemanticRule, CancellationToken cancellationToken)
        {
            if (Verbose)
            {
                FormatLogger.WriteLine("  {0}", globalSemanticRule.GetType().Name);
            }

            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId);
                var syntaxRoot = await GetSyntaxRootAndFilter(globalSemanticRule, document, cancellationToken).ConfigureAwait(false);
                if (syntaxRoot == null)
                {
                    continue;
                }

                StartDocument();
                solution = await globalSemanticRule.ProcessAsync(document, syntaxRoot, cancellationToken).ConfigureAwait(false);
                EndDocument(document);
            }

            return solution;
        }
    }
}
