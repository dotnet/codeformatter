// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using CommandLine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.CodeFormatting;
using Microsoft.DotNet.CodeFormatter.Analyzers;
using Microsoft.CodeAnalysis.Options;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeFormatter
{
    internal static class Program
    {
        private const int FAILED = 1;
        private const int SUCCEEDED = 0;

        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<
                ListOptions,
                ExportOptions,
                FormatOptions,
                AnalyzeOptions>(args)
            .MapResult(
                (ListOptions listOptions) => RunListCommand(listOptions),
                (ExportOptions exportOptions) => RunExportOptionsCommand(exportOptions),
                (FormatOptions formatOptions) => RunFormatCommand(formatOptions),
                (AnalyzeOptions analyzeOptions) => RunAnalyzeCommand(analyzeOptions),
                errs => FAILED);
        }

        private static int RunExportOptionsCommand(ExportOptions exportOptions)
        {
            int result = FAILED;
            PropertyBag allOptions = OptionsHelper.BuildDefaultPropertyBag();

            allOptions.SaveTo(exportOptions.OutputPath, id: "codeformatter-options");
            Console.WriteLine("Options file saved to: " + Path.GetFullPath(exportOptions.OutputPath));

            result = SUCCEEDED;

            return result;
        }

        private static int RunListCommand(ListOptions options)
        {
            // If user did not explicitly reference either analyzers or
            // rules in list command, we will dump both sets.
            if (!options.Analyzers && !options.Rules)
            {
                options.Analyzers = true;
                options.Rules = true;
            }

            ListRulesAndAnalyzers(options.Analyzers, options.Rules);

            return SUCCEEDED;
        }

        private static void ListRulesAndAnalyzers(bool listAnalyzers, bool listRules)
        {
            Console.WriteLine("{0,-20} {1}", "Name", "Title");
            Console.WriteLine("==============================================");

            if (listAnalyzers)
            {
                ImmutableArray<DiagnosticDescriptor> diagnosticDescriptors = FormattingEngine.GetSupportedDiagnostics(OptionsHelper.DefaultCompositionAssemblies);
                foreach (var diagnosticDescriptor in diagnosticDescriptors)
                {
                    Console.WriteLine("{0,-20} :{1}", diagnosticDescriptor.Id, diagnosticDescriptor.Title);
                }
            }

            if (listRules)
            {
                var rules = FormattingEngine.GetFormattingRules();
                foreach (var rule in rules)
                {
                    Console.WriteLine("{0,-20} :{1}", rule.Name, rule.Description);
                }
            }
        }
        private static int RunAnalyzeCommand(AnalyzeOptions options)
        {
            return RunCommand(options, false);
        }

        private static int RunFormatCommand(FormatOptions options)
        {
            return RunCommand(options, true);
        }

        private static int RunCommand(CommandLineOptions options, bool applyCodeFixes) { 
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                RunAsync(options, ct).Wait(ct);
                Console.WriteLine("Completed formatting.");
                return SUCCEEDED;
            }
            catch (AggregateException ex)
            {
                var typeLoadException = ex.InnerExceptions.FirstOrDefault() as ReflectionTypeLoadException;
                if (typeLoadException == null)
                    throw;

                Console.WriteLine("ERROR: Type loading error detected. In order to run this tool you need either Visual Studio 2015 or Microsoft Build Tools 2015 tools installed.");
                var messages = typeLoadException.LoaderExceptions.Select(e => e.Message).Distinct();
                foreach (var message in messages)
                    Console.WriteLine("- {0}", message);

                return FAILED;
            }
        }

        private static IAnalyzerAssemblyLoader _analyzerAssemblyLoader;
        private static IAnalyzerAssemblyLoader AnalyzerAssemblyLoader {
            get
            {
                if(_analyzerAssemblyLoader == null)
                {
                    // use Roslyn's existing simple loader, there're no special requirements for our usage
                    var loaderAssembly = Assembly.Load((typeof(CommandLineProject)).Assembly.FullName);
                    _analyzerAssemblyLoader = (IAnalyzerAssemblyLoader)Activator.CreateInstance(loaderAssembly.GetType("Microsoft.CodeAnalysis.SimpleAnalyzerAssemblyLoader"));
                }
                return _analyzerAssemblyLoader;
            }
        }

        private static ImmutableArray<DiagnosticAnalyzer> LoadAnalyzersFromAssembly(string path, bool throwIfNoAnalyzersFound)
        {
            var analyzerRef = new AnalyzerFileReference(path, AnalyzerAssemblyLoader);
            var newAnalyzers = analyzerRef.GetAnalyzersForAllLanguages();
            if (newAnalyzers.Count() == 0 && throwIfNoAnalyzersFound)
            {
                throw new Exception(String.Format("Specified analyzer assembly {0} contained no analyzers", analyzerRef.GetAssembly().FullName));
            }
            return newAnalyzers;
        }

        private static async Task<int> RunAsync(CommandLineOptions options, CancellationToken cancellationToken)
        {
            var assemblies = OptionsHelper.DefaultCompositionAssemblies;
            var engine = FormattingEngine.Create(assemblies);

            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            configBuilder.Add(options.PreprocessorConfigurations.ToArray());            
            engine.PreprocessorConfigurations = configBuilder.ToImmutableArray();

            engine.FormattingOptionsFilePath = options.OptionsFilePath;
            engine.Verbose = options.Verbose;
            engine.AllowTables = options.DefineDotNetFormatter;
            engine.FileNames = options.FileFilters.ToImmutableArray();
            engine.CopyrightHeader = options.CopyrightHeaderText;
            engine.ApplyFixes = options.ApplyFixes;
            engine.LogOutputPath = options.LogOutputPath;

            if (options.AnalyzerListFile != null && options.AnalyzerListText != null && options.AnalyzerListText.Count() > 0)
            {
                foreach (var analyzerPath in options.AnalyzerListText)
                {
                    if (File.Exists(analyzerPath))
                    {
                        var newAnalyzers = LoadAnalyzersFromAssembly(analyzerPath, true);
                        engine.AddAnalyzers(newAnalyzers);
                    }
                    else if (Directory.Exists(analyzerPath))
                    {
                        var DLLs = Directory.GetFiles(analyzerPath, "*.dll");
                        foreach (var dll in DLLs)
                        {
                            // allows specifying a folder that contains analyzers as well as non-analyzer DLLs without throwing
                            var newAnalyzers = LoadAnalyzersFromAssembly(dll, false);
                            if (newAnalyzers.Count() > 0)
                            {
                                engine.AddAnalyzers(newAnalyzers);
                            }
                        }
                    }
                }
            }

            // Analyzers will hydrate rule enabled/disabled settings
            // directly from the options referenced by file path
            // in options.OptionsFilePath
            if (!options.UseAnalyzers)
            {
                if (!SetRuleMap(engine, options.RuleMap))
                {
                    return FAILED;
                }
            }

            foreach (var item in options.Targets)
            {
                await RunItemAsync(engine, item, options.Language, options.UseAnalyzers, cancellationToken);
            }

            return SUCCEEDED;
        }

        private static async Task RunItemAsync(
            IFormattingEngine engine,
            string item,
            string language,
            bool useAnalyzers,
            CancellationToken cancellationToken)
        {
            Console.WriteLine(Path.GetFileName(item));
            string extension = Path.GetExtension(item);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".rsp"))
            {
                using (var workspace = ResponseFileWorkspace.Create())
                {
                    Project project = workspace.OpenCommandLineProject(item, language);
                    await engine.FormatProjectAsync(project, useAnalyzers, cancellationToken);
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var solution = await workspace.OpenSolutionAsync(item, cancellationToken);
                    await engine.FormatSolutionAsync(solution, useAnalyzers, cancellationToken);
                }
            }
            else
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var project = await workspace.OpenProjectAsync(item, cancellationToken);
                    await engine.FormatProjectAsync(project, useAnalyzers, cancellationToken);
                }
            }
        }

        private static bool SetRuleMap(IFormattingEngine engine, ImmutableDictionary<string, bool> ruleMap)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            foreach (var entry in ruleMap)
            {
                var rule = engine.AllRules.Where(x => comparer.Equals(x.Name, entry.Key)).FirstOrDefault();
                if (rule == null)
                {
                    Console.WriteLine("Could not find rule with name {0}", entry.Key);
                    return false;
                }

                engine.ToggleRuleEnabled(rule, entry.Value);
            }
            Debug.Assert(ruleMap.Count == engine.AllRules.Count());

            return true;
        }
    }
}
