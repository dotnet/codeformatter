using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.DeadCodeAnalysis;

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
            var ignoredSymbols = new HashSet<string>();

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg[0] == '/' || arg[0] == '-')
                {
                    string argName = arg.Substring(1);
                    if (argName.Equals("ignore", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (++i < args.Length)
                        {
                            var symbolList = args[i].Split(';', ',', ' ', '\t', '\n');
                            foreach (var symbol in symbolList)
                            {
                                ignoredSymbols.Add(symbol.Trim());
                            }
                        }
                        else
                        {
                            PrintUsage();
                            return -1;
                        }
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
            else
            {
                
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DeadRegions <project> [<project> ...] [/file <file>] [/nodisabled] [/inactive] [/ignore <symbol list>] [/define <symbol list>] [/disable <symbol list>] [/edit] [@<response file>]");
        }
    }
}
