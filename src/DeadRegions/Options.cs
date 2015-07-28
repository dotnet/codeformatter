// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.DotNet.DeadRegionAnalysis;

namespace DeadRegions
{
    internal class Options
    {
        private static readonly char[] s_symbolSeparatorChars = new[] { ';', ',' };

        private OptionParser _parser;
        private List<string> _ignoredSymbols;
        private List<string> _definedSymbols;
        private List<string> _disabledSymbols;
        private List<IEnumerable<string>> _symbolConfigurations;

        public string Usage { get { return _parser.Usage; } }

        public ImmutableArray<string> FilePaths { get; private set; }

        public IEnumerable<string> IgnoredSymbols { get { return _ignoredSymbols; } }

        public IEnumerable<string> DefinedSymbols { get { return _definedSymbols; } }

        public IEnumerable<string> DisabledSymbols { get { return _disabledSymbols; } }

        public IEnumerable<IEnumerable<string>> SymbolConfigurations { get { return _symbolConfigurations; } }

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
                arg => _symbolConfigurations.Add(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Specify a complete symbol configuration",
                allowMultiple: true);

            _parser.Add(
                "ignore",
                arg => _ignoredSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Ignore a list of symbols (treat as varying)",
                allowMultiple: true);

            _parser.Add(
                "define",
                arg => _definedSymbols.AddRange(ParseSymbolList(arg)),
                parameterUsage: "<symbol list>",
                description: "Define a list of symbols (treat as always true)",
                allowMultiple: true);

            _parser.Add(
                "disable",
                arg => _disabledSymbols.AddRange(ParseSymbolList(arg)),
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
        }

        public bool Parse()
        {
            try
            {
                _ignoredSymbols = new List<string>();
                _definedSymbols = new List<string>();
                _disabledSymbols = new List<string>();
                _symbolConfigurations = new List<IEnumerable<string>>();
                UndefinedSymbolValue = Tristate.Varying;
                FilePaths = _parser.Parse(Environment.CommandLine);
            }
            catch (OptionParseException e)
            {
                Console.WriteLine("error: " + e.Message);
                return false;
            }

            return FilePaths.Length > 0;
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
