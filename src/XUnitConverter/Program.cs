// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;

namespace XUnitConverter
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("xunitconverter <project>");
                return;
            }

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += delegate { cts.Cancel(); };
            RunAsync(args[0], cts.Token).Wait();
        }

        private static async Task RunAsync(string projectPath, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;

            var project = await workspace.OpenProjectAsync(projectPath, cancellationToken);
            var converters = new ConverterBase[]
                {
                    new MSTestToXUnitConverter(),
                    new TestAssertTrueOrFalseConverter(),
                    new AssertArgumentOrderConverter(),
                };

            foreach (var converter in converters)
            {
                var solution = await converter.ProcessAsync(project, cancellationToken);
                project = solution.GetProject(project.Id);
            }

            workspace.TryApplyChanges(project.Solution);
        }
    }
}
