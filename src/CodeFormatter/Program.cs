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

        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("CodeFormatter <project, solution or responsefile> [/file:<filename>] [/nocopyright] [/nounicode] [/tables] [/c:<config1,config2> [/copyright:file] [/lang:<language>] [/verbose]");
                Console.WriteLine("    <filename>   - Only apply changes to files with specified name.");
                Console.WriteLine("    <configs>    - Additional preprocessor configurations the formatter");
                Console.WriteLine("                   should run under.");
                Console.WriteLine("    <copyright>  - Specifies file containing copyright header.");
                Console.WriteLine("                   Use ConvertTests to convert MSTest tests to xUnit.");
                Console.WriteLine("    <tables>     - Let tables opt out of formatting by defining DOTNET_FORMATTER");
                Console.WriteLine("    <language>   - Specifies the language to use when a responsefile is specified.");
                Console.WriteLine("                   i.e. 'C#', 'Visual Basic', ... (default: 'C#')");
                Console.WriteLine("    <verbose>    - Verbose output");
                Console.WriteLine("    <nounicode>  - Do not convert unicode strings to escape sequences");
                Console.WriteLine("    <nocopyright>- Do not update the copyright message.");
                return -1;
            }

            var projectOrSolutionPath = args[0];
            if (!File.Exists(projectOrSolutionPath))
            {
                Console.Error.WriteLine("Project, solution or response file {0} doesn't exist.", projectOrSolutionPath);
                return -1;
            }

            var fileNamesBuilder = ImmutableArray.CreateBuilder<string>();
            var ruleTypeBuilder = ImmutableArray.CreateBuilder<string>();
            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            var copyrightHeader = FormattingConstants.DefaultCopyrightHeader;
            var language = LanguageNames.CSharp;
            var convertUnicode = true;
            var allowTables = false;
            var verbose = false;
            var comparer = StringComparer.OrdinalIgnoreCase;

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
                    copyrightHeader = ImmutableArray<string>.Empty;
                }
                else if (comparer.Equals(arg, "/nounicode"))
                {
                    convertUnicode = false;
                }
                else if (comparer.Equals(arg, "/verbose"))
                {
                    verbose = true;
                }
                else if (comparer.Equals(arg, "/tables"))
                {
                    allowTables = true;
                }
                else
                {
                    ruleTypeBuilder.Add(arg);
                }
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                RunAsync(
                    projectOrSolutionPath,
                    ruleTypeBuilder.ToImmutableArray(),
                    fileNamesBuilder.ToImmutableArray(),
                    configBuilder.ToImmutableArray(),
                    copyrightHeader,
                    language,
                    allowTables,
                    convertUnicode,
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

        private static async Task RunAsync(
            string projectSolutionOrRspPath,
            ImmutableArray<string> ruleTypes,
            ImmutableArray<string> fileNames,
            ImmutableArray<string[]> preprocessorConfigurations,
            ImmutableArray<string> copyrightHeader,
            string language,
            bool allowTables,
            bool convertUnicode,
            bool verbose,
            CancellationToken cancellationToken)
        {
            var engine = FormattingEngine.Create(ruleTypes);
            engine.PreprocessorConfigurations = preprocessorConfigurations;
            engine.FileNames = fileNames;
            engine.CopyrightHeader = copyrightHeader;
            engine.AllowTables = allowTables;
            engine.ConvertUnicodeCharacters = convertUnicode;
            engine.Verbose = verbose;

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
        }
    }
}
