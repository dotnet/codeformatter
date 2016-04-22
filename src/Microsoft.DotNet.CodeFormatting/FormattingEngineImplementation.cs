// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.DotNet.CodeFormatter.Analyzers;

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
        private IEnumerable<DiagnosticAnalyzer> _analyzers;
        private readonly IEnumerable<IOptionsProvider> _optionsProviders;
        private readonly IEnumerable<ExportFactory<ISyntaxFormattingRule, SyntaxRule>> _syntaxRules;
        private readonly IEnumerable<ExportFactory<ILocalSemanticFormattingRule, LocalSemanticRule>> _localSemanticRules;
        private readonly IEnumerable<ExportFactory<IGlobalSemanticFormattingRule, GlobalSemanticRule>> _globalSemanticRules;
        private readonly Stopwatch _watch = new Stopwatch();
        private readonly Dictionary<string, bool> _ruleEnabledMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly ImmutableDictionary<string, CodeFixProvider> _diagnosticIdToFixerMap;
        private CompilationWithAnalyzers _compilationWithAnalyzers;
        private object _outputLogger; // Microsoft.CodeAnalysis.ErrorLogger
        private Assembly _loggerAssembly;

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

        public bool ApplyFixes { get; set; }

        public string LogOutputPath { get; set; }

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
            => _analyzers
                    .SelectMany(a => a.SupportedDiagnostics)
                    .OrderBy(a => a.Id)
                    .ToImmutableArray();

        // Use the Roslyn ErrorLogger type to log diagnostics per analyzer to SARIF format
        public void LogDiagnostics(string filePath, ImmutableArray<Diagnostic> diagnostics)
        {
            if (_loggerAssembly == null)
            {
                _loggerAssembly = Assembly.Load(typeof(CommandLineParser).Assembly.FullName);
            }

            _outputLogger = Activator.CreateInstance(
                _loggerAssembly.GetType("Microsoft.CodeAnalysis.ErrorLogger"),
                new object[] { new FileStream(filePath, FileMode.Append, FileAccess.Write), "CodeFormatter", "0.1", _loggerAssembly.GetName().Version });

            var logDiagMethodInfo = _outputLogger.GetType().GetMethod("LogDiagnostic", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var diagnostic in diagnostics)
            {
                logDiagMethodInfo.Invoke(_outputLogger, new object[] { diagnostic, System.Globalization.CultureInfo.DefaultThreadCurrentCulture });
            }

            ((IDisposable)_outputLogger).Dispose();
        }

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
            _analyzers = analyzers;
            _fixers = fixers;
            _syntaxRules = syntaxRules;
            _optionsProviders = optionProviders;
            _localSemanticRules = localSemanticRules;
            _globalSemanticRules = globalSemanticRules;

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
            var watch = new Stopwatch();
            watch.Start();

            var workspace = solution.Workspace;
            foreach (var projectId in solution.ProjectIds)
            {
                var project = workspace.CurrentSolution.GetProject(projectId);
                await FormatProjectWithAnalyzersAsync(project, cancellationToken).ConfigureAwait(false);
            }

            watch.Stop();
            FormatLogger.WriteLine("Total time {0}", watch.Elapsed);
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

            var workspace = project.Solution.Workspace;

            await FormatProjectWithSyntaxAnalyzersAsync(workspace, project.Id, cancellationToken);
            await FormatProjectWithLocalAnalyzersAsync(workspace, project.Id, cancellationToken);
            await FormatProjectWithGlobalAnalyzersAsync(workspace, project.Id, cancellationToken);
            await FormatProjectWithUnspecifiedAnalyzersAsync(workspace, project.Id, cancellationToken);

            watch.Stop();
            FormatLogger.WriteLine("Total time for formatting {0} - {1}", project.Name, watch.Elapsed);
        }

        private async Task FormatProjectWithSyntaxAnalyzersAsync(Workspace workspace, ProjectId projectId, CancellationToken cancellationToken)
        {
            var analyzers = _analyzers.Where(a => a.SupportedDiagnostics.All(d => d.CustomTags.Contains(RuleType.Syntactic)));
            await FormatWithAnalyzersCoreAsync(workspace, projectId, analyzers, cancellationToken);
        }

        private async Task FormatProjectWithLocalAnalyzersAsync(Workspace workspace, ProjectId projectId, CancellationToken cancellationToken)
        {
            var analyzers = _analyzers.Where(a => a.SupportedDiagnostics.All(d => d.CustomTags.Contains(RuleType.LocalSemantic)));
            await FormatWithAnalyzersCoreAsync(workspace, projectId, analyzers, cancellationToken);
        }

        private async Task FormatProjectWithGlobalAnalyzersAsync(Workspace workspace, ProjectId projectId, CancellationToken cancellationToken)
        {
            var analyzers = _analyzers.Where(a => a.SupportedDiagnostics.All(d => d.CustomTags.Contains(RuleType.GlobalSemantic)));

            // Since global analyzers can potentially conflict with each other, run them one by one.
            foreach (var analyzer in analyzers)
            {
                await FormatWithAnalyzersCoreAsync(workspace, projectId, new[] { analyzer }, cancellationToken);
            }
        }

        private async Task FormatProjectWithUnspecifiedAnalyzersAsync(Workspace workspace, ProjectId projectId, CancellationToken cancellationToken)
        {
            var analyzers = _analyzers.Where(a => a.SupportedDiagnostics.All(d => {
                return !(d.CustomTags.Contains(RuleType.Syntactic) || d.CustomTags.Contains(RuleType.LocalSemantic) || d.CustomTags.Contains(RuleType.GlobalSemantic));
            }));

            // Treat analyzers with unknown rule types as if they were global in case they might conflict with each other
            foreach (var analyzer in analyzers)
            {
                await FormatWithAnalyzersCoreAsync(workspace, projectId, new[] { analyzer }, cancellationToken);
            }
        }

        private async Task FormatWithAnalyzersCoreAsync(Workspace workspace, ProjectId projectId, IEnumerable<DiagnosticAnalyzer> analyzers, CancellationToken cancellationToken)
        {
            if (analyzers != null && analyzers.Count() != 0)
            {
                var project = workspace.CurrentSolution.GetProject(projectId);
                var diagnostics = await GetDiagnostics(project, analyzers, cancellationToken).ConfigureAwait(false);
                // Ensure at least 1 analyzer supporting the current project's language ran
                if (_compilationWithAnalyzers != null)
                {
                    var extension = StringComparer.OrdinalIgnoreCase.Equals(project.Language, "C#") ? ".csproj" : ".vbproj";
                    var resultFile = project.FilePath.Substring(project.FilePath.LastIndexOf(Path.DirectorySeparatorChar)).Replace(extension, "_CodeFormatterResults.txt");

                    foreach (var analyzer in analyzers)
                    {
                        var diags = await _compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(ImmutableArray.Create(analyzer), cancellationToken);

                        if (Verbose || LogOutputPath != null)
                        {
                            var analyzerTelemetryInfo = await _compilationWithAnalyzers.GetAnalyzerTelemetryInfoAsync(analyzer, cancellationToken);
                            FormatLogger.WriteLine("{0}\t{1}\t{2}\t{3}", project.Name, analyzer.ToString(), diags.Count(), analyzerTelemetryInfo.ExecutionTime);
                            var resultPath = Path.ChangeExtension(LogOutputPath + resultFile, "json");                            
                            LogDiagnostics(resultPath, diags);
                        }
                    }
                }

                if (ApplyFixes)
                {
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
                            operation.Apply(workspace, cancellationToken);
                        }
                    }
                }
            }
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

        private async Task<ImmutableArray<Diagnostic>> GetDiagnostics(Project project, IEnumerable<DiagnosticAnalyzer> analyzers, CancellationToken cancellationToken)
        {
            AnalyzerOptions analyzerOptions = null;

            if (!string.IsNullOrEmpty(FormattingOptionsFilePath))
            {
                var additionalTextFile = new AdditionalTextFile(FormattingOptionsFilePath);
                analyzerOptions = new AnalyzerOptions(new AdditionalText[] { additionalTextFile }.ToImmutableArray());
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

            // If there are any compilation errors then some of the semantic rules will be faulty. Log the
            // compiler errors and the metadatareferences to diagnose the issues.
            if (Verbose)
            {
                var compilerDiagnostics = compilation.GetDiagnostics().Where(d => d.Severity != DiagnosticSeverity.Hidden && d.Severity != DiagnosticSeverity.Info);
                if (compilerDiagnostics.Any())
                {

                    Console.WriteLine("Error count " + compilerDiagnostics.Count());

                    Console.WriteLine("Metadata References for {0}", project.Name);
                    foreach (var mr in project.MetadataReferences)
                    {
                        Console.WriteLine(mr.Display);
                    }

                    Console.WriteLine();
                    Console.WriteLine("Diagnostics for {0}", project.Name);

                    foreach (var diag in compilerDiagnostics)
                    {
                        Console.WriteLine(diag.ToString());
                    }
                }
            }

            IEnumerable<DiagnosticAnalyzer> analyzersToRun = analyzers.Where(a => a.SupportsLanguage(project.Language));

            if (analyzersToRun.Any())
            {
                var compilationWithAnalyzers = compilation.WithAnalyzers(analyzersToRun.ToImmutableArray(), analyzerOptions);
                _compilationWithAnalyzers = compilationWithAnalyzers;
                return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
            }
            else
            {
                _compilationWithAnalyzers = null;
                return ImmutableArray<Diagnostic>.Empty;
            }
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

                if (!localSemanticRule.SupportsLanguage(document.Project.Language))
                {
                    continue;
                }

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

        public void AddAnalyzers(ImmutableArray<DiagnosticAnalyzer> analyzers)
        {
            var toAdd = new List<DiagnosticAnalyzer>();
            foreach (var analyzer in analyzers)
            {
                IEqualityComparer<DiagnosticAnalyzer> comparer = new AnalyzerComparer();
                if (!_analyzers.Contains(analyzer, comparer))
                {
                    toAdd.Add(analyzer);
                }
            }
            _analyzers = _analyzers.Concat(toAdd.ToArray());
        }
    }

    // Simple comparer to ensure we don't add the same analyzer twice given large lists of analyzer assemblies
    internal class AnalyzerComparer : IEqualityComparer<DiagnosticAnalyzer>
    {
        public bool Equals(DiagnosticAnalyzer x, DiagnosticAnalyzer y)
        {
            return x.ToString() == y.ToString();
        }

        public int GetHashCode(DiagnosticAnalyzer obj)
        {
            return obj.GetHashCode();
        }
    }
}