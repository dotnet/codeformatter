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
        private static bool s_printSymbolInfo;
        private static bool s_edit;

        private static List<string> s_filePaths = new List<string>();
        private static List<string> s_ignoredSymbols = new List<string>();
        private static List<string> s_definedSymbols = new List<string>();
        private static List<string> s_disabledSymbols = new List<string>();
        private static List<IEnumerable<string>> s_symbolConfigurations = new List<IEnumerable<string>>();
        private static Tristate s_undefinedSymbolValue = Tristate.Varying;

        private static readonly char[] s_symbolSeparatorChars = new[] { ';', ',' };
        private static readonly char[] s_valueIndicatorChars = new[] { '=', ':' };

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

            if (!ParseOptions(args))
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
                alwaysIgnoredSymbols: s_ignoredSymbols,
                undefinedSymbolValue: s_undefinedSymbolValue);

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

        private static bool ParseOptions(string[] args)
        {
            Dictionary<string, Action<string>> options = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "config", arg => s_symbolConfigurations.Add(ParseSymbolList(arg)) },
                { "ignore", arg => s_ignoredSymbols.AddRange(ParseSymbolList(arg)) },
                { "define", arg => s_definedSymbols.AddRange(ParseSymbolList(arg)) },
                { "disable", arg => s_disabledSymbols.AddRange(ParseSymbolList(arg)) },
                { "default", arg => s_undefinedSymbolValue = Tristate.Parse(arg) },
                { "printdisabled", arg => s_printDisabled = true },
                { "printenabled", arg => s_printEnabled = true },
                { "printvarying", arg => s_printVarying = true },
                { "printsymbols", arg => s_printSymbolInfo = true },
                { "print", arg => s_printDisabled = s_printEnabled = s_printVarying = s_printSymbolInfo = true },
                { "edit", arg => s_edit = true }
            };

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg[0] == '@')
                {
                    string responseFilePath = arg.Substring(1);
                    // TODO: Better tokenization of arguments (e.g. allow single/double quotes)
                    // Could parse Environment.CommandLine with a big regex
                    string[] responseFileArgs = File.ReadAllText(responseFilePath).Replace("\r", string.Empty).Split(' ', '\t', '\n');
                    return ParseOptions(responseFileArgs);
                }
                else if (arg[0] == '/' || arg[0] == '-')
                {
                    string optionName = arg.Substring(1);

                    Action<string> action;
                    if (!options.TryGetValue(optionName, out action))
                    {
                        int separatorIndex = optionName.IndexOfAny(s_valueIndicatorChars);
                        if (separatorIndex == -1)
                        {
                            if (++i >= args.Length)
                            {
                                Console.WriteLine("error: missing argument for option: " + optionName);
                                return false;
                            }

                            arg = args[i];
                        }
                        else
                        {
                            arg = optionName.Substring(separatorIndex + 1);
                            optionName = optionName.Substring(0, separatorIndex);
                        }
                    }

                    if (options.TryGetValue(optionName, out action))
                    {
                        try
                        {
                            action(arg);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("error: failed to parse option: {0}: {1}", optionName, e.Message);
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("error: unrecognized option: " + optionName);
                        return false;
                    }
                }
                else
                {
                    s_filePaths.Add(arg);
                }
            }

            if (s_filePaths.Count == 0)
            {
                Console.WriteLine("error: must specify at least one project file or source file");
                return false;
            }

            return true;
        }

        private static string[] ParseSymbolList(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new FormatException("Symbol list must not be empty");
            }

            return s.Split(s_symbolSeparatorChars);
        }

        private static async Task RunAsync(CancellationToken cancellationToken)
        {
            var regionInfos = await s_engine.GetConditionalRegionInfo(cancellationToken);
            PrintConditionalRegionInfo(regionInfos);

            if (s_printSymbolInfo)
            {
                Console.WriteLine();
                PrintSymbolInfo();
            }
        }

        private static async Task OnDocumentAnalyzed(DocumentConditionalRegionInfo info, CancellationToken cancellationToken)
        {
            if (s_edit)
            {
                var fileInfo = new FileInfo(info.Document.FilePath);
                if (fileInfo.IsReadOnly || !fileInfo.Exists)
                {
                    Console.WriteLine("warning: skipping document '{0}' because it {1}.",
                        info.Document.FilePath, fileInfo.IsReadOnly ? "is read-only" : "does not exist");
                    return;
                }

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

OPTIONS
  /config  <symbol list>
  /ignore  <symbol list>
  /define  <symbol list>
  /disable <symbol list>
  /default <false|true|varying>
  /printenabled
  /printdisabled
  /printvarying
  /print
  /edit
  @<response file>

NOTES
  <symbol list> is a comma or semi-colon separated list of preprocessor symbols");
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
                Console.WriteLine("Conditional Regions");
                Console.WriteLine("  {0,5} found in total", totalRegionCount);

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

        private static void PrintSymbolInfo()
        {
            Console.WriteLine("Symbols");
            Console.WriteLine("  {0,5} unique symbol(s) specified: {1}", s_engine.SpecifiedSymbols.Count(), string.Join(";", s_engine.SpecifiedSymbols));
            Console.WriteLine("  {0,5} unique symbol(s) visited: {1}", s_engine.VisitedSymbols.Count(), string.Join(";", s_engine.VisitedSymbols));
            Console.WriteLine("  {0,5} specified symbol(s) unvisited: {1}", s_engine.UnvisitedSymbols.Count(), string.Join(";", s_engine.UnvisitedSymbols));
        }
    }
}
