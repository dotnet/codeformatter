// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DeadRegionAnalysis;
using System.Threading;
using System.IO;

namespace DeadRegions
{
    internal class DeadRegions
    {
        private static readonly char[] s_symbolSeparatorChars = new[] { ';', ',' };

        private static OptionParser s_options = new OptionParser();
        private static AnalysisEngine s_engine;

        private static IList<string> s_filePaths;
        private static List<string> s_ignoredSymbols = new List<string>();
        private static List<string> s_definedSymbols = new List<string>();
        private static List<string> s_disabledSymbols = new List<string>();
        private static List<IEnumerable<string>> s_symbolConfigurations = new List<IEnumerable<string>>();
        private static Tristate s_undefinedSymbolValue = Tristate.Varying;

        private static bool s_printDisabled;
        private static bool s_printEnabled;
        private static bool s_printVarying;
        private static bool s_printSymbolInfo;
        private static bool s_edit;

        private static int s_disabledCount = 0;
        private static int s_enabledCount = 0;
        private static int s_varyingCount = 0;

        public static int Main(string[] args)
        {
            s_options.Add(
                "config",
                arg => s_symbolConfigurations.Add(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Specify a complete symbol configuration",
                allowMultiple: true);

            s_options.Add(
                "ignore",
                arg => s_ignoredSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Ignore a list of symbols (treat as varying)",
                allowMultiple: true);

            s_options.Add(
                "define",
                arg => s_definedSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Define a list of symbols (treat as always true)",
                allowMultiple: true);

            s_options.Add(
                "disable",
                arg => s_disabledSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Disable a list of symbols (treat as always disabled)",
                allowMultiple: true);

            s_options.Add(
                "default",
                arg => s_undefinedSymbolValue = Tristate.Parse(arg),
                parameterUsage: "<false|true|varying>",
                description: "Set the default value for symbols which do not have a specified value (defaults to varying)");

            s_options.Add(
                "printdisabled",
                () => s_printDisabled = true,
                description: "Print the list of always disabled conditional regions");

            s_options.Add(
                "printenabled",
                () => s_printEnabled = true,
                description: "Print the list of always enabled conditional regions");

            s_options.Add(
                "printvarying",
                () => s_printVarying = true,
                description: "Print the list of varying conditional regions");

            s_options.Add(
                "printsymbols",
                () => s_printSymbolInfo = true,
                description: "Print the lists of uniquely specified preprocessor symbols, symbols visited during analysis, and symbols not encountered during analysis");

            s_options.Add(
                "print",
                () => s_printDisabled = s_printEnabled = s_printVarying = s_printSymbolInfo = true,
                description: "Print the entire list of conditional regions and the lists of preprocessor symbols (combination of printenabled, printdisabled, printvarying, and printsymbols)");

            s_options.Add(
                "edit",
                () => s_edit = true,
                "Perform edits to remove always enabled and always disabled conditional regions from source files, and simplify preprocessor expressions which evaluate to 'varying'");

            try
            {
                s_filePaths = s_options.Parse(Environment.CommandLine);
            }
            catch (OptionParseException e)
            {
                Console.WriteLine("error: " + e.Message);
                PrintUsage();
                return -1;
            }

            if (s_filePaths.Count < 1)
            {
                PrintUsage();
                return -1;
            }

            s_engine = AnalysisEngine.FromFilePaths(
                s_filePaths,
                symbolConfigurations: s_symbolConfigurations,
                alwaysDefinedSymbols: s_definedSymbols,
                alwaysDisabledSymbols: s_disabledSymbols,
                alwaysIgnoredSymbols: s_ignoredSymbols,
                undefinedSymbolValue: s_undefinedSymbolValue);

            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            Console.CancelKeyPress += delegate { cts.Cancel(); };

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

        private static void PrintUsage()
        {
            Console.WriteLine(
@"SYNTAX
  DeadRegions [<project> ...] [options]
  DeadRegions [<source file> ...] [options]
");

            Console.WriteLine(s_options.Usage);

            Console.WriteLine(
@"NOTES
  <symbol list> is a comma or semi-colon separated list of preprocessor symbols");
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
                document = await s_engine.SimplifyVaryingPreprocessorExpressions(document, cancellationToken);

                var text = await document.GetTextAsync(cancellationToken);
                using (var file = File.Open(document.FilePath, FileMode.Truncate, FileAccess.Write))
                {
                    var writer = new StreamWriter(file, text.Encoding);
                    text.Write(writer, cancellationToken);
                    await writer.FlushAsync();
                }
            }
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
                        if (region.State == Tristate.False)
                        {
                            s_disabledCount++;
                            Console.ForegroundColor = ConsoleColor.Blue;
                            if (s_printDisabled)
                            {
                                Console.WriteLine(region);
                            }
                        }
                        else if (region.State == Tristate.True)
                        {
                            s_enabledCount++;
                            Console.ForegroundColor = ConsoleColor.Green;
                            if (s_printEnabled)
                            {
                                Console.WriteLine(region);
                            }
                        }
                        else
                        {
                            s_varyingCount++;
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            if (s_printVarying)
                            {
                                Console.WriteLine(region);
                            }
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
                    Console.WriteLine("  {0,5} always disabled", s_disabledCount);
                }

                if (s_enabledCount > 0)
                {
                    Console.WriteLine("  {0,5} always enabled", s_enabledCount);
                }

                if (s_varyingCount > 0)
                {
                    Console.WriteLine("  {0,5} varying", s_varyingCount);
                }
            }

            // TODO: Lines of disabled/enabled/varying code. This involves calculating unnecessary regions, converting those to line spans.
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
