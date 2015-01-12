using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.DeadCodeAnalysis;
using System.Threading;

namespace DeadCode
{
    // TODO: Rename this to "DeadRegions". Rename namespaces to ConditionalRegionAnalysis? ConditionalRegionAnalysisEngine?
    // to allow for more dead code analysis based on roslyn which is not about proprocessor regions.
    internal class DeadCode
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return -1;
            }

            var projectPaths = new List<string>();
            IEnumerable<string> ignoredSymbols = null;
            IEnumerable<string> definedSymbols = null;

            bool edit = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg[0] == '/' || arg[0] == '-')
                {
                    string argName = arg.Substring(1);
                    if (argName.Equals("ignore", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            ignoredSymbols = args[i].Split(';', ',', ' ', '\t', '\n');
                        }
                        else
                        {
                            PrintUsage();
                            return -1;
                        }
                    }
                    else if (argName.Equals("define", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            definedSymbols = args[i].Split(';', ',', ' ', '\t', '\n');
                        }
                        else
                        {
                            PrintUsage();
                            return -1;
                        }
                    }
                    else if (argName.Equals("edit", StringComparison.InvariantCultureIgnoreCase))
                    {
                        edit = true;
                    }
                }
                else
                {
                    projectPaths.Add(arg);
                }
            }

            if (projectPaths.Count == 0)
            {
                PrintUsage();
                return -1;
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            Console.CancelKeyPress += delegate { cts.Cancel(); };

            Console.WriteLine("Analyzing...");

            try
            {
                // TODO: Clean this up.
                var createTask = AnalysisEngine.Create(projectPaths, definedSymbols, ignoredSymbols, ct);
                createTask.Wait(ct);

                var analysisEngine = createTask.Result;
                
                if (edit)
                {
                    analysisEngine.RemoveUnnecessaryConditionalRegions(ct).Wait(ct);
                }
                else
                {
                    analysisEngine.PrintConditionalRegionInfoAsync(ct).Wait(ct);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 1;
            }

            Console.WriteLine("Done.");
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DeadRegions <project> [<project> ...] [/file <file>] [/printsummary] [/printenabled] [/printdisabled] [/printvarying] [/ignore <symbol list>] [/define <symbol list>] [/edit] [@<response file>]");
        }
    }
}
