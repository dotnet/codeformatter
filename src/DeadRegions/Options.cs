using Microsoft.DotNet.DeadRegionAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeadRegions
{
    internal class Options
    {
        private static readonly char[] s_symbolSeparatorChars = new[] { ';', ',' };

        private OptionParser _parser;

        public string Usage { get { return _parser.Usage; } }

        public IList<string> FilePaths { get; private set; }

        public readonly List<string> IgnoredSymbols = new List<string>();

        public readonly List<string> DefinedSymbols = new List<string>();

        public readonly List<string> DisabledSymbols = new List<string>();

        public readonly List<IEnumerable<string>> SymbolConfigurations = new List<IEnumerable<string>>();

        public Tristate UndefinedSymbolValue { get; private set; }

        public bool PrintDisabled { get; private set; }

        public bool PrintEnabled { get; private set; }

        public bool PrintVarying { get; private set; }

        public bool PrintSymbolInfo { get; private set; }

        public bool Edit { get; private set; }

        public Options()
        {
            _parser = new OptionParser();

            _parser.Add(
                "config",
                arg => SymbolConfigurations.Add(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Specify a complete symbol configuration",
                allowMultiple: true);

            _parser.Add(
                "ignore",
                arg => IgnoredSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Ignore a list of symbols (treat as varying)",
                allowMultiple: true);

            _parser.Add(
                "define",
                arg => DefinedSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Define a list of symbols (treat as always true)",
                allowMultiple: true);

            _parser.Add(
                "disable",
                arg => DisabledSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Disable a list of symbols (treat as always disabled)",
                allowMultiple: true);

            _parser.Add(
                "default",
                arg => UndefinedSymbolValue = Tristate.Parse(arg),
                parameterUsage: "<false|true|varying>",
                description: "Set the default value for symbols which do not have a specified value (defaults to varying)");

            _parser.Add(
                "printdisabled",
                () => PrintDisabled = true,
                description: "Print the list of always disabled conditional regions");

            _parser.Add(
                "printenabled",
                () => PrintEnabled = true,
                description: "Print the list of always enabled conditional regions");

            _parser.Add(
                "printvarying",
                () => PrintVarying = true,
                description: "Print the list of varying conditional regions");

            _parser.Add(
                "printsymbols",
                () => PrintSymbolInfo = true,
                description: "Print the lists of uniquely specified preprocessor symbols, symbols visited during analysis, and symbols not encountered during analysis");

            _parser.Add(
                "print",
                () => PrintDisabled = PrintEnabled = PrintVarying = PrintSymbolInfo = true,
                description: "Print the entire list of conditional regions and the lists of preprocessor symbols (combination of printenabled, printdisabled, printvarying, and printsymbols)");

            _parser.Add(
                "edit",
                () => Edit = true,
                "Perform edits to remove always enabled and always disabled conditional regions from source files, and simplify preprocessor expressions which evaluate to 'varying'");

            UndefinedSymbolValue = Tristate.Varying;
        }

        public bool Parse()
        {
            try
            {
                FilePaths = _parser.Parse(Environment.CommandLine);
            }
            catch (OptionParseException e)
            {
                Console.WriteLine("error: " + e.Message);
                return false;
            }

            return FilePaths.Count > 0;
        }

        private static string[] ParseSymbolList(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new FormatException("Symbol list must not be empty");
            }

            return s.Split(s_symbolSeparatorChars);
        }
    }
}
