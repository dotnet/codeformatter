using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.DeadRegionAnalysis;
using System.Threading;
using System.IO;

namespace DeadRegions
{
    internal class DeadRegions
    {
        private static AnalysisEngine s_engine;
        private static bool s_printDisabled;
        private static bool s_printEnabled;
        private static bool s_printVarying;
        private static bool s_edit;

        private static List<string> s_filePaths = new List<string>();
        private static IEnumerable<string> s_ignoredSymbols = null;
        private static IEnumerable<string> s_definedSymbols = null;
        private static IEnumerable<string> s_disabledSymbols = null;
        private static List<IEnumerable<string>> s_symbolConfigurations = new List<IEnumerable<string>>();

        private static readonly char[] symbolSeparatorChars = new[] { ';', ',', ' ', '\t', '\n' };

        private static int s_disabledCount = 0;
        private static int s_enabledCount = 0;
        private static int s_varyingCount = 0;

        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return -1;
            }

            if (!ParseArguments(args))
            {
                PrintUsage();
                return -1;
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            Console.CancelKeyPress += delegate { cts.Cancel(); };

            s_engine = AnalysisEngine.FromFilePaths(
                s_filePaths,
                symbolConfigurations: s_symbolConfigurations,
                alwaysDefinedSymbols: s_definedSymbols,
                alwaysDisabledSymbols: s_disabledSymbols,
                alwaysIgnoredSymbols: s_ignoredSymbols);

            try
            {
                s_engine.DocumentAnalyzed += OnDocumentAnalyzed;
                RunAsync(ct).Wait(ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 1;
            }

            return 0;
        }

        private static bool ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg[0] == '@')
                {
                    string responseFilePath = arg.Substring(1);
                    // TODO: Better tokenization of arguments (e.g. allow single/double quotes)
                    string[] responseFileArgs = File.ReadAllText(responseFilePath).Replace("\r", string.Empty).Split(' ', '\t', '\n');
                    return ParseArguments(responseFileArgs);
                }
                else if (arg[0] == '/' || arg[0] == '-')
                {
                    string argName = arg.Substring(1);
                    if (argName.Equals("ignore", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            s_ignoredSymbols = args[i].Split(symbolSeparatorChars);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (argName.Equals("define", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            s_definedSymbols = args[i].Split(symbolSeparatorChars);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (argName.Equals("disable", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            s_disabledSymbols = args[i].Split(symbolSeparatorChars);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (argName.Equals("config", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            var config = args[i].Split(symbolSeparatorChars);
                            s_symbolConfigurations.Add(config);
                        }
                        else
                        {
                            return false;
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
                    else if (argName.Equals("print", StringComparison.InvariantCultureIgnoreCase))
                    {
                        s_printDisabled = true;
                        s_printEnabled = true;
                        s_printVarying = true;
                    }
                    else if (argName.Equals("edit", StringComparison.InvariantCultureIgnoreCase))
                    {
                        s_edit = true;
                    }
                }
                else
                {
                    s_filePaths.Add(arg);
                }
            }

            if (s_filePaths.Count == 0)
            {
                return false;
            }

            return true;
        }

        private static async Task RunAsync(CancellationToken cancellationToken)
        {
            var regionInfos = await s_engine.GetConditionalRegionInfo(cancellationToken);
            PrintConditionalRegionInfo(regionInfos);
        }

        private static async Task OnDocumentAnalyzed(DocumentConditionalRegionInfo info, CancellationToken cancellationToken)
        {
            if (s_edit)
            {
                var document = await s_engine.RemoveUnnecessaryRegions(info, cancellationToken);

                var text = await document.GetTextAsync(cancellationToken);
                using (var file = File.Open(document.FilePath, FileMode.Truncate, FileAccess.Write))
                {
                    var writer = new StreamWriter(file, text.Encoding);
                    text.Write(writer, cancellationToken);
                    await writer.FlushAsync();
                }
            }
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
  /print
  /edit
  @<response file>");
        }

        private static void PrintConditionalRegionInfo(IEnumerable<DocumentConditionalRegionInfo> regionInfos)
        {
            var originalForegroundColor = Console.ForegroundColor;

            foreach (var info in regionInfos)
            {
                foreach (var chain in info.Chains)
                {
                    foreach (var region in chain.Regions)
                    {
                        switch (region.State)
                        {
                            case ConditionalRegionState.AlwaysDisabled:
                                s_disabledCount++;
                                Console.ForegroundColor = ConsoleColor.Blue;
                                if (s_printDisabled)
                                {
                                    Console.WriteLine(region);
                                }
                                break;
                            case ConditionalRegionState.AlwaysEnabled:
                                s_enabledCount++;
                                Console.ForegroundColor = ConsoleColor.Green;
                                if (s_printEnabled)
                                {
                                    Console.WriteLine(region);
                                }
                                break;
                            case ConditionalRegionState.Varying:
                                s_varyingCount++;
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
            int totalRegionCount = s_disabledCount + s_enabledCount + s_varyingCount;
            if (totalRegionCount == 0)
            {
                Console.WriteLine("Did not find any conditional regions.");
            }
            else
            {
                Console.WriteLine("Found");
                Console.WriteLine("  {0,5} conditional regions total", totalRegionCount);

                if (s_disabledCount > 0)
                {
                    Console.WriteLine("  {0,5} disabled", s_disabledCount);
                }

                if (s_enabledCount > 0)
                {
                    Console.WriteLine("  {0,5} enabled", s_enabledCount);
                }

                if (s_varyingCount > 0)
                {
                    Console.WriteLine("  {0,5} varying", s_varyingCount);
                }
            }

            // TODO: Lines of dead code.  A chain struct might be useful because there are many operations on a chain.
            // This involves calculating unnecessary regions, converting those to line spans
        }
    }
}
