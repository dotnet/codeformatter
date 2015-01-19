using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.DeadCodeAnalysis;
using System.Threading;
using System.IO;

namespace DeadCode
{
    // TODO: Rename this to "DeadRegions". Rename namespaces to ConditionalRegionAnalysis? ConditionalRegionAnalysisEngine?
    // to allow for more dead code analysis based on roslyn which is not about proprocessor regions.
    internal class DeadCode
    {
        private static AnalysisEngine _engine;
        private static bool s_printDisabled;
        private static bool s_printEnabled;
        private static bool s_printVarying;
        private static bool s_edit;

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
                        s_printDisabled = true;
                    }
                    else if (argName.Equals("printenabled", StringComparison.InvariantCultureIgnoreCase))
                    {
                        s_printEnabled = true;
                    }
                    else if (argName.Equals("printvarying", StringComparison.InvariantCultureIgnoreCase))
                    {
                        s_printVarying = true;
                    }
                    else if (argName.Equals("edit", StringComparison.InvariantCultureIgnoreCase))
                    {
                        s_edit = true;
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

            _engine = AnalysisEngine.FromFilePaths(
                projectPaths,
                alwaysDefinedSymbols: definedSymbols,
                alwaysIgnoredSymbols: ignoredSymbols);

            try
            {
                RunAsync(ct).Wait(ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 1;
            }

            return 0;
        }

        private static async Task RunAsync(CancellationToken cancellationToken)
        {
            var regionInfo = await _engine.GetConditionalRegionInfo(cancellationToken);

            if (s_edit)
            {
                foreach (var info in regionInfo)
                {
                    var document = await _engine.RemoveUnnecessaryRegions(info, cancellationToken);

                    var text = await document.GetTextAsync(cancellationToken);
                    using (var file = File.Open(document.FilePath, FileMode.Truncate, FileAccess.Write))
                    {
                        var writer = new StreamWriter(file, text.Encoding);
                        text.Write(writer, cancellationToken);
                    }
                }
            }

            PrintConditionalRegionInfo(regionInfo);
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

        private static void PrintConditionalRegionInfo(IEnumerable<DocumentConditionalRegionInfo> regionInfo)
        {
            var originalForegroundColor = Console.ForegroundColor;

            int disabledCount = 0;
            int enabledCount = 0;
            int varyingCount = 0;

            foreach (var info in regionInfo)
            {
                foreach (var chain in info.Chains)
                {
                    foreach (var region in chain.Regions)
                    {
                        switch (region.State)
                        {
                            case ConditionalRegionState.AlwaysDisabled:
                                disabledCount++;
                                Console.ForegroundColor = ConsoleColor.Blue;
                                if (s_printDisabled)
                                {
                                    Console.WriteLine(region);
                                }
                                break;
                            case ConditionalRegionState.AlwaysEnabled:
                                enabledCount++;
                                Console.ForegroundColor = ConsoleColor.Green;
                                if (s_printEnabled)
                                {
                                    Console.WriteLine(region);
                                }
                                break;
                            case ConditionalRegionState.Varying:
                                varyingCount++;
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                if (s_printVarying)
                                {
                                    Console.WriteLine(region);
                                }
                                break;
                        }
                    }
                }
            }

            Console.ForegroundColor = originalForegroundColor;

            // Print summary
            Console.WriteLine();

            int totalRegionCount = disabledCount + enabledCount + varyingCount;
            if (totalRegionCount == 0)
            {
                Console.WriteLine("Did not find any conditional regions.");
            }

            Console.WriteLine("Found");
            Console.WriteLine("  {0,5} conditional regions total", totalRegionCount);

            if (disabledCount > 0)
            {
                Console.WriteLine("  {0,5} disabled", disabledCount);
            }

            if (enabledCount > 0)
            {
                Console.WriteLine("  {0,5} enabled", enabledCount);
            }

            if (varyingCount > 0)
            {
                Console.WriteLine("  {0,5} varying", varyingCount);
            }

            // TODO: Lines of dead code.  A chain struct might be useful because there are many operations on a chain.
            // This involves calculating unnecessary regions, converting those to line spans
        }
    }
}
