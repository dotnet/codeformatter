using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            var xunitConverter = new XUnitConverter();
            var solution = await xunitConverter.ProcessAsync(project, cancellationToken);
            workspace.TryApplyChanges(solution);
        }
    }
}
