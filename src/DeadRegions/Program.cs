// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.DeadRegionAnalysis;

namespace DeadRegions
{
    internal class DeadRegions
    {
        public static int Main(string[] args)
        {
            var options = new Options();
            if (!options.Parse())
            {
                PrintUsage(options);
                return -1;
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                var engine = AnalysisEngine.FromFilePaths(
                    options.FilePaths,
                    symbolConfigurations: options.SymbolConfigurations,
                    alwaysDefinedSymbols: options.DefinedSymbols,
                    alwaysDisabledSymbols: options.DisabledSymbols,
                    alwaysIgnoredSymbols: options.IgnoredSymbols,
                    undefinedSymbolValue: options.UndefinedSymbolValue,
                    cancellationToken: ct).Result;

                engine.DocumentAnalyzed += (analysisEngine, info, cancellationToken) => OnDocumentAnalyzed(analysisEngine, options, info, cancellationToken);
                RunAsync(engine, options, ct).Wait(ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 1;
            }

            return 0;
        }

        private static void PrintUsage(Options options)
        {
            Console.WriteLine(
@"SYNTAX
  DeadRegions [<project> ...] [options]
  DeadRegions [<source file> ...] [options]
");

            Console.WriteLine(options.Usage);

            Console.WriteLine(
@"NOTES
  * The (possibly) environment dependent set of preprocessor symbols for a
    given project determined by MsBuild when loading an input project file will
    be ignored; only explicitly specified build configurations will be used in
    analysis.

  * When multiple projects are specified as input, the intersection of the
    files in all the projects will be analyzed.

  * <symbol list> is a comma or semi-colon separated list of preprocessor
    symbols.");
        }

        private static async Task RunAsync(AnalysisEngine engine, Options options, CancellationToken cancellationToken)
        {
            var regionInfos = await engine.GetConditionalRegionInfo(cancellationToken);

            PrintConditionalRegionInfo(options, regionInfos);

            if (options.PrintSymbolInfo)
            {
                Console.WriteLine();
                PrintSymbolInfo(engine);
            }
        }

        private static async Task OnDocumentAnalyzed(AnalysisEngine engine, Options options, DocumentConditionalRegionInfo info, CancellationToken cancellationToken)
        {
            if (options.Edit)
            {
                var fileInfo = new FileInfo(info.Document.FilePath);
                if (fileInfo.IsReadOnly || !fileInfo.Exists)
                {
                    Console.WriteLine("warning: skipping document '{0}' because it {1}.",
                        info.Document.FilePath, fileInfo.IsReadOnly ? "is read-only" : "does not exist");
                    return;
                }

                var document = await engine.RemoveUnnecessaryRegions(info, cancellationToken);
                document = await engine.SimplifyVaryingPreprocessorExpressions(document, cancellationToken);

                var text = await document.GetTextAsync(cancellationToken);
                using (var file = File.Open(document.FilePath, FileMode.Truncate, FileAccess.Write))
                {
                    var writer = new StreamWriter(file, text.Encoding);
                    text.Write(writer, cancellationToken);
                    await writer.FlushAsync();
                }
            }
        }

        private static void PrintConditionalRegionInfo(Options options, IEnumerable<DocumentConditionalRegionInfo> regionInfos)
        {
            int disabledCount = 0;
            int enabledCount = 0;
            int varyingCount = 0;

            var originalForegroundColor = Console.ForegroundColor;

            foreach (var info in regionInfos)
            {
                foreach (var chain in info.Chains)
                {
                    foreach (var region in chain.Regions)
                    {
                        if (region.State == Tristate.False)
                        {
                            disabledCount++;
                            Console.ForegroundColor = ConsoleColor.Blue;
                            if (options.PrintDisabled)
                            {
                                Console.WriteLine(region);
                            }
                        }
                        else if (region.State == Tristate.True)
                        {
                            enabledCount++;
                            Console.ForegroundColor = ConsoleColor.Green;
                            if (options.PrintEnabled)
                            {
                                Console.WriteLine(region);
                            }
                        }
                        else
                        {
                            varyingCount++;
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            if (options.PrintVarying)
                            {
                                Console.WriteLine(region);
                            }
                        }
                    }
                }
            }

            Console.ForegroundColor = originalForegroundColor;

            // Print summary
            int totalRegionCount = disabledCount + enabledCount + varyingCount;
            if (totalRegionCount == 0)
            {
                Console.WriteLine("Did not find any conditional regions.");
            }
            else
            {
                Console.WriteLine("Conditional Regions");
                Console.WriteLine("  {0,5} found in total", totalRegionCount);

                if (disabledCount > 0)
                {
                    Console.WriteLine("  {0,5} always disabled", disabledCount);
                }

                if (enabledCount > 0)
                {
                    Console.WriteLine("  {0,5} always enabled", enabledCount);
                }

                if (varyingCount > 0)
                {
                    Console.WriteLine("  {0,5} varying", varyingCount);
                }
            }

            // TODO: Lines of disabled/enabled/varying code. This involves calculating unnecessary regions, converting those to line spans.
        }

        private static void PrintSymbolInfo(AnalysisEngine engine)
        {
            Console.WriteLine("Symbols");
            Console.WriteLine("  {0,5} unique symbol(s) specified: {1}", engine.SpecifiedSymbols.Count(), string.Join(";", engine.SpecifiedSymbols));
            Console.WriteLine("  {0,5} unique symbol(s) visited: {1}", engine.VisitedSymbols.Count(), string.Join(";", engine.VisitedSymbols));
            Console.WriteLine("  {0,5} specified symbol(s) unvisited: {1}", engine.UnvisitedSymbols.Count(), string.Join(";", engine.UnvisitedSymbols));
        }
    }
}
