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
                Console.Error.WriteLine("CodeFormatter <solution> [<rule types>]");
                return -1;
            }

            var solutionPath = args[0];
            if (!File.Exists(solutionPath))
            {
                Console.Error.WriteLine("Solution {0} doesn't exist.", solutionPath);
                return -1;
            }

            var ruleTypes = args.Skip(1);

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            RunAsync(solutionPath, ruleTypes, ct).Wait(ct);
            Console.WriteLine("Completed formatting.");
            return 0;
        }

        private static async Task RunAsync(string solutionFilePath, IEnumerable<string> ruleTypes, CancellationToken cancellationToken)
        {
            try
            {
                var workspace = MSBuildWorkspace.Create();
                await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken);

                var engine = FormattingEngine.Create(ruleTypes);
                await engine.RunAsync(workspace, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
