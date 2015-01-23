// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
                Console.WriteLine("CodeFormatter <project or solution> [<rule types>] [/file <filename>]");
                Console.WriteLine("    <rule types> - Rule types to use in addition to the default ones.");
                Console.WriteLine("                   Use ConvertTests to convert MSTest tests to xUnit.");
                Console.WriteLine("    <filename> - Only apply changes to files with specified name.");
                return -1;
            }

            var projectOrSolutionPath = args[0];
            if (!File.Exists(projectOrSolutionPath))
            {
                Console.Error.WriteLine("Project or solution {0} doesn't exist.", projectOrSolutionPath);
                return -1;
            }

            List<string> ruleTypes = new List<string>();
            List<string> filenames = new List<string>();

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("/file", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        string param = args[i + 1];
                        filenames.Add(param);
                        i++;
                    }
                }
                else
                {
                    ruleTypes.Add(arg);
                }
            }


            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            RunAsync(projectOrSolutionPath, ruleTypes, filenames, ct).Wait(ct);
            Console.WriteLine("Completed formatting.");
            return 0;
        }

        private static async Task RunAsync(string projectOrSolutionPath, IEnumerable<string> ruleTypes, IEnumerable<string> filenames, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            var engine = FormattingEngine.Create(ruleTypes, filenames);
            engine.Verbose = true;

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
