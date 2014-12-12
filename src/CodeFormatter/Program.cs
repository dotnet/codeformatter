// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition.Hosting;
using System.IO;
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
            if (args.Length != 1)
            {
                Console.Error.WriteLine("CodeFormatter <solution>");
                return -1;
            }

            var solutionPath = args[0];
            if (!File.Exists(solutionPath))
            {
                Console.Error.WriteLine("Solution {0} doesn't exist.", solutionPath);
                return -1;
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            RunAsync(solutionPath, ct).Wait(ct);
            Console.WriteLine("Completed formatting.");
            return 0;
        }

        private static async Task RunAsync(string solutionFilePath, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken);

            var engine = FormattingEngine.Create();
            await engine.RunAsync(workspace, cancellationToken);
        }
    }
}
