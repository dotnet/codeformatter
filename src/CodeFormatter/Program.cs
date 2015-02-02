// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.CodeFormatting;

namespace CodeFormatter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("CodeFormatter <project or solution> [<rule types>] [/file:<filename>] [/nocopyright] [/c:<config1,config2>");
                Console.WriteLine("    <rule types> - Rule types to use in addition to the default ones.");
                Console.WriteLine("                   Use ConvertTests to convert MSTest tests to xUnit.");
                Console.WriteLine("    <filename>   - Only apply changes to files with specified name.");
                Console.WriteLine("    <configs>    - Additional preprocessor configurations the formatter")
                Console.WriteLine("                   should run under");
                return -1;
            }

            var projectOrSolutionPath = args[0];
            if (!File.Exists(projectOrSolutionPath))
            {
                Console.Error.WriteLine("Project or solution {0} doesn't exist.", projectOrSolutionPath);
                return -1;
            }

            var fileNamesBuilder = ImmutableArray.CreateBuilder<string>();
            var ruleTypeBuilder = ImmutableArray.CreateBuilder<string>();
            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            var comparer = StringComparer.OrdinalIgnoreCase;
            var disableCopyright = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("/file:", StringComparison.OrdinalIgnoreCase))
                {
                    var all = arg.Substring(6);
                    var files = all.Split(new[] { ','}, StringSplitOptions.RemoveEmptyEntries);
                    fileNamesBuilder.AddRange(files);
                }
                else if (arg.StartsWith("/c:", StringComparison.OrdinalIgnoreCase))
                {
                    var all = arg.Substring(3);
                    var configs = all.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    configBuilder.Add(configs);
                }
                else if (comparer.Equals(arg, "/nocopyright"))
                {
                    disableCopyright = true;
                }
                else
                {
                    ruleTypeBuilder.Add(arg);
                }
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            RunAsync(
                projectOrSolutionPath, 
                ruleTypeBuilder.ToImmutableArray(),
                fileNamesBuilder.ToImmutableArray(),
                configBuilder.ToImmutableArray(),
                disableCopyright, 
                ct).Wait(ct);
            Console.WriteLine("Completed formatting.");
            return 0;
        }

        private static async Task RunAsync(
            string projectOrSolutionPath, 
            ImmutableArray<string> ruleTypes, 
            ImmutableArray<string> fileNames, 
            ImmutableArray<string[]> preprocessorConfigurations, 
            bool disableCopright, 
            CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            var engine = FormattingEngine.Create(ruleTypes);
            engine.PreprocessorConfigurations = preprocessorConfigurations;
            engine.FileNames = fileNames;

            if (disableCopright)
            {
                engine.CopyrightHeader = ImmutableArray<string>.Empty;
            }

            string extension = Path.GetExtension(projectOrSolutionPath);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
            {
                var solution = await workspace.OpenSolutionAsync(projectOrSolutionPath, cancellationToken);
                await engine.FormatSolutionAsync(solution, cancellationToken);
            }
            else
            {
                workspace.LoadMetadataForReferencedProjects = true;
                var project = await workspace.OpenProjectAsync(projectOrSolutionPath, cancellationToken);
                await engine.FormatProjectAsync(project, cancellationToken);
            }
        }
    }
}
