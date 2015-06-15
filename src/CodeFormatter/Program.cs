// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.CodeFormatting;

namespace CodeFormatter
{
    internal static class Program
    {
        private const string FileSwitch = "/file:";
        private const string ConfigSwitch = "/c:";
        private const string CopyrightSwitch = "/copyright:";
        private const string LanguageSwitch = "/lang:";
        private const string RuleEnabledSwitch1 = "/rule+:";
        private const string RuleEnabledSwitch2 = "/rule:";
        private const string RuleDisabledSwitch = "/rule-:";

        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine(
@"CodeFormatter <project, solution or responsefile> [/file:<filename>] 
    [/lang:<language>] [/c:<config>[,<config>...]>]
    [/copyright:<file> | /nocopyright] [/tables] [/nounicode] 
    [/rule(+|-):rule1,rule2,...
    [/verbose]

    /file        - Only apply changes to files with specified name.
    /lang        - Specifies the language to use when a responsefile is
                   specified. i.e. 'C#', 'Visual Basic', ... (default: 'C#')
    /c           - Additional preprocessor configurations the formatter
                   should run under.
    /copyright   - Specifies file containing copyright header.
                   Use ConvertTests to convert MSTest tests to xUnit.
    /nocopyright - Do not update the copyright message.
    /tables      - Let tables opt out of formatting by defining
                   DOTNET_FORMATTER
    /nounicode   - Do not convert unicode strings to escape sequences
    /rule(+|-)   - Enable (default) or disable the specified rule
    /rules       - List the available rules
    /verbose     - Verbose output
");
                return -1;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var projectOrSolutionPath = args[0];
            if (comparer.Equals(projectOrSolutionPath, "/rules"))
            {
                RunListRules();
                return 0;
            }

            if (!File.Exists(projectOrSolutionPath))
            {
                Console.Error.WriteLine("Project, solution or response file {0} doesn't exist.", projectOrSolutionPath);
                return -1;
            }

            var fileNamesBuilder = ImmutableArray.CreateBuilder<string>();
            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            var copyrightHeader = FormattingDefaults.DefaultCopyrightHeader;
            var ruleMap = ImmutableDictionary<string, bool>.Empty;
            var language = LanguageNames.CSharp;
            var allowTables = false;
            var verbose = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith(FileSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    var all = arg.Substring(FileSwitch.Length);
                    var files = all.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    fileNamesBuilder.AddRange(files);
                }
                else if (arg.StartsWith(ConfigSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    var all = arg.Substring(ConfigSwitch.Length);
                    var configs = all.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    configBuilder.Add(configs);
                }
                else if (arg.StartsWith(CopyrightSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = arg.Substring(CopyrightSwitch.Length);
                    try
                    {
                        copyrightHeader = ImmutableArray.CreateRange(File.ReadAllLines(fileName));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Could not read {0}", fileName);
                        Console.Error.WriteLine(ex.Message);
                        return -1;
                    }
                }
                else if (arg.StartsWith(LanguageSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    language = arg.Substring(LanguageSwitch.Length);
                }
                else if (comparer.Equals(arg, "/nocopyright"))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.CopyrightRuleName, false);
                }
                else if (comparer.Equals(arg, "/nounicode"))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.UnicodeLiteralsRuleName, false);
                }
                else if (comparer.Equals(arg, "/verbose"))
                {
                    verbose = true;
                }
                else if (comparer.Equals(arg, RuleEnabledSwitch1))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleEnabledSwitch1.Length), enabled: true);
                }
                else if (comparer.Equals(arg, RuleEnabledSwitch2))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleEnabledSwitch2.Length), enabled: true);
                }
                else if (comparer.Equals(arg, RuleDisabledSwitch))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleDisabledSwitch.Length), enabled: false);
                }
                else if (comparer.Equals(arg, "/tables"))
                {
                    allowTables = true;
                }
                else
                {
                    Console.WriteLine("Unrecognized option: {0}", arg);
                    return 1;
                }
            }

            return RunFormat(
                    projectOrSolutionPath,
                    fileNamesBuilder.ToImmutableArray(),
                    configBuilder.ToImmutableArray(),
                    copyrightHeader,
                    ruleMap,
                    language,
                    allowTables,
                    verbose);
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

        private static void UpdateRuleMap(ref ImmutableDictionary<string, bool> ruleMap, string data, bool enabled)
        {
            foreach (var current in data.Split(','))
            {
                ruleMap = ruleMap.SetItem(current, enabled);
            }
        }

        private static int RunFormat(
            string projectSolutionOrRspPath,
            ImmutableArray<string> fileNames,
            ImmutableArray<string[]> preprocessorConfigurations,
            ImmutableArray<string> copyrightHeader,
            ImmutableDictionary<string, bool> ruleMap,
            string language,
            bool allowTables,
            bool verbose)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                RunFormatAsync(
                    projectSolutionOrRspPath,
                    fileNames,
                    preprocessorConfigurations,
                    copyrightHeader,
                    ruleMap,
                    language,
                    allowTables,
                    verbose,
                    ct).Wait(ct);
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

        private static async Task<int> RunFormatAsync(
            string projectSolutionOrRspPath,
            ImmutableArray<string> fileNames,
            ImmutableArray<string[]> preprocessorConfigurations,
            ImmutableArray<string> copyrightHeader,
            ImmutableDictionary<string, bool> ruleMap,
            string language,
            bool allowTables,
            bool verbose,
            CancellationToken cancellationToken)
        {
            var engine = FormattingEngine.Create();
            engine.PreprocessorConfigurations = preprocessorConfigurations;
            engine.FileNames = fileNames;
            engine.CopyrightHeader = copyrightHeader;
            engine.AllowTables = allowTables;
            engine.Verbose = verbose;

            if (!SetRuleMap(engine, ruleMap))
            {
                return 1;
            }

            Console.WriteLine(Path.GetFileName(projectSolutionOrRspPath));
            string extension = Path.GetExtension(projectSolutionOrRspPath);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".rsp"))
            {
                using (var workspace = ResponseFileWorkspace.Create())
                {
                    Project project = workspace.OpenCommandLineProject(projectSolutionOrRspPath, language);
                    await engine.FormatProjectAsync(project, cancellationToken);
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var solution = await workspace.OpenSolutionAsync(projectSolutionOrRspPath, cancellationToken);
                    await engine.FormatSolutionAsync(solution, cancellationToken);
                }
            }
            else
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var project = await workspace.OpenProjectAsync(projectSolutionOrRspPath, cancellationToken);
                    await engine.FormatProjectAsync(project, cancellationToken);
                }
            }

            return 0;
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
    }
}
