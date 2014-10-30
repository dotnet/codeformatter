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

            RunAsnc(solutionPath, ct).Wait(ct);
            return 0;
        }

        private static async Task RunAsnc(string solutionFilePath, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken);

            var catalog = new AssemblyCatalog(typeof(Program).Assembly);
            var container = new CompositionContainer(catalog);
            var engine = container.GetExportedValue<IFormattingEngine>();
            await engine.RunAsync(workspace, cancellationToken);
        }
    }
}
