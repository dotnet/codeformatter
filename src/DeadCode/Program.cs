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

            bool printDisabled = false;
            bool printEnabled = false;
            bool printVarying = false;
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
                    else if (argName.Equals("printdisabled", StringComparison.InvariantCultureIgnoreCase))
                    {
                        printDisabled = true;
                    }
                    else if (argName.Equals("printenabled", StringComparison.InvariantCultureIgnoreCase))
                    {
                        printEnabled = true;
                    }
                    else if (argName.Equals("printvarying", StringComparison.InvariantCultureIgnoreCase))
                    {
                        printVarying = true;
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

            var engine = AnalysisEngine.FromFilePaths(
                projectPaths,
                alwaysDefinedSymbols: definedSymbols,
                alwaysIgnoredSymbols: ignoredSymbols,
                printDisabled: printDisabled,
                printEnabled: printEnabled,
                printVarying: printVarying,
                edit: edit);

            try
            {
                engine.RunAsync(ct).Wait(ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 1;
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
@"
SYNTAX
  DeadRegions [<project> ...] [options]
  DeadRegions [<source file> ...] [options]

PARAMETERS
  /config  <symbol list>
  /ignore  <symbol list>
  /define  <symbol list>
  /disable <symbol list>
  /printenabled
  /printdisabled
  /printvarying
  /edit
  @<response file>");
        }
    }
}
