// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EditorConfig.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Options;
using Microsoft.DotNet.CodeFormatting;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CodeFormatter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var result = CommandLineParser.Parse(args);
            if (result.IsError)
            {
                Console.Error.WriteLine(result.Error);
                CommandLineParser.PrintUsage();
                return -1;
            }

            var options = result.Options;
            int exitCode;
            switch (options.Operation)
            {
                case Operation.ShowHelp:
                    CommandLineParser.PrintUsage();
                    exitCode = 0;
                    break;

                case Operation.ListRules:
                    RunListRules();
                    exitCode = 0;
                    break;
                case Operation.Format:
                    exitCode = RunFormat(options);
                    break;
                default:
                    throw new Exception("Invalid enum value: " + options.Operation);
            }

            return exitCode;
        }

        private static void RunListRules()
        {
            var rules = FormattingEngine.GetFormattingRules();
            Console.WriteLine("{0,-20} {1}", "Name", "Description");
            Console.WriteLine("==============================================");
            foreach (var rule in rules)
            {
                Console.WriteLine("{0,-20} :{1}", rule.Name, rule.Description);
            }
        }

        private static int RunFormat(CommandLineOptions options)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                RunFormatAsync(options, ct).Wait(ct);
                Console.WriteLine("Completed formatting.");
                return 0;
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

                return 1;
            }
        }

        private static async Task<int> RunFormatAsync(CommandLineOptions options, CancellationToken cancellationToken)
        {
            var engine = FormattingEngine.Create();
            engine.PreprocessorConfigurations = options.PreprocessorConfigurations;
            engine.FileNames = options.FileNames;
            engine.CopyrightHeader = options.CopyrightHeader;
            engine.AllowTables = options.AllowTables;
            engine.Verbose = options.Verbose;

            if (!SetRuleMap(engine, options.RuleMap))
            {
                return 1;
            }

            foreach (var item in options.FormatTargets)
            {
                await RunFormatItemAsync(engine, item, options, cancellationToken);
            }

            return 0;
        }

        private static async Task RunFormatItemAsync(IFormattingEngine engine, string item, CommandLineOptions options, CancellationToken cancellationToken)
        {
            Console.WriteLine(Path.GetFileName(item));
            string extension = Path.GetExtension(item);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".rsp"))
            {
                using (var workspace = ResponseFileWorkspace.Create())
                {
                    ConfigureWorkspace(workspace, options, item);

                    Project project = workspace.OpenCommandLineProject(item, options.Language);
                    await engine.FormatProjectAsync(project, cancellationToken);
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    ConfigureWorkspace(workspace, options, item);

                    workspace.LoadMetadataForReferencedProjects = true;
                    var solution = await workspace.OpenSolutionAsync(item, cancellationToken);
                    await engine.FormatSolutionAsync(solution, cancellationToken);
                }
            }
            else
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    ConfigureWorkspace(workspace, options, item);

                    workspace.LoadMetadataForReferencedProjects = true;
                    var project = await workspace.OpenProjectAsync(item, cancellationToken);
                    await engine.FormatProjectAsync(project, cancellationToken);
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

            return true;
        }

        static void ConfigureWorkspace(Workspace workspace, CommandLineOptions options, string item)
        {
            if(options.UseEditorConfig)
                ConfigureWorkspaceWithEditorConfig(workspace, options, item);
        }

        static void ConfigureWorkspaceWithEditorConfig(Workspace workspace, CommandLineOptions options, string item)
        {
            // Save the current directory to restore it after using the EditorConfigParser
            var previousCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                // Since the EditorConfigParser nuget searches for the .editorconfig hierarchy using
                // the current directory, we need to change it to the item path in order for it to find it
                Directory.SetCurrentDirectory(Path.GetDirectoryName(item));

                var editorConfigParser = new EditorConfigParser();
                var editorConfigItems = default(IEnumerable<FileConfiguration>);

                if (options.Language == LanguageNames.CSharp)
                    editorConfigItems = editorConfigParser.Parse(".cs");
                else if (options.Language == LanguageNames.VisualBasic)
                    editorConfigItems = editorConfigParser.Parse(".vb");
                else
                    return;

                // Microsoft.CodeAnalysis.Options.IOptionService
                var optionService = workspace
                    .AsDynamicReflection()
                    ._workspaceOptionService;

                // Get the registered options in the roslyn workspace
                IEnumerable<IOption> registeredOptions = optionService.GetRegisteredOptions();

                foreach (var option in registeredOptions.Where(x => x.StorageLocations != null))
                {
                    // Get the EditorConfig storage of the option
                    OptionStorageLocation editorConfigStorageLocation = option
                        .StorageLocations
                        .FirstOrDefault(x => x.GetType().Name == "EditorConfigStorageLocation`1");

                    // If it's null, it means that the option in the workspace does not have a corresponding storage in the .editorconfig file.
                    if (editorConfigStorageLocation != null)
                    {
                        string editorConfigKey = editorConfigStorageLocation.AsDynamicReflection().KeyName;

                        // Get the value in the .editorconfig associated with the editorConfig storage key
                        string editorConfigValue =
                            (from editorConfigItem in editorConfigItems
                             from prop in editorConfigItem.Properties
                             where prop.Key == editorConfigKey
                             select prop.Value).FirstOrDefault();

                        if (!string.IsNullOrEmpty(editorConfigValue))
                        {
                            // Map the value in the .editorconfig file to the Option value in the roslyn workspace
                            // by invoking Microsoft.CodeAnalysis.Options.EditorConfigStorageLocation<T>.TryOption(...) 
                            object optionValue = default(object);
                            if (editorConfigStorageLocation.AsDynamicReflection().TryGetOption(
                                    option,
                                    new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                                    {
                                        { editorConfigKey, editorConfigValue }
                                    }),
                                    option.Type,
                                    OutValue.Create<object>(x => optionValue = x)))
                            {
                                var optionKey = new OptionKey(
                                    option,
                                    option.IsPerLanguage ? options.Language : null);

                                Console.WriteLine($"Applying {editorConfigKey}={editorConfigValue} setting into {option.Name}={optionValue}...");
                                // And finally set the option value in the workspace
                                workspace.Options = workspace.Options.WithChangedOption(optionKey, optionValue);
                            }
                        }
                    }
                }
            }
            finally
            {
                // And restore the original current directory
                Directory.SetCurrentDirectory(previousCurrentDirectory);
            }
        }
    }
}
