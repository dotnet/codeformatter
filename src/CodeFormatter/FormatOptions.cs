// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

using CommandLine;

using Microsoft.DotNet.CodeFormatting;

namespace CodeFormatter
{
    [Verb("format", HelpText = "Apply code formatting rules and analyzers to specified project.")]
    internal class FormatOptions
    {
        [Value(
            0,
            HelpText = "Project, solution or response file to drive code formatting.", 
            Required = true)]
        public IEnumerable<string> FormatTargets { get; set; }

        [Option(
            'o',
            "options-file-path",
            HelpText = "Path to an options file that should be used to configure analysis")]
        public string OptionsFilePath { get; set; }
   
        [Option(
            "file-filters", 
            HelpText = "Only apply changes to files with specified name(s).", 
            Separator = ',')]
        public IEnumerable<string> FileFilters { get; set; }

        [Option(
            'l', "lang", 
            HelpText = "Specifies the language to use when a response file is specified, e.g., 'C#', 'Visual Basic', ... (default: 'C#').")]
        public string Language { get; set; }

        [Option(
            'c', "configs", 
            HelpText = "Comma-separated list of preprocessor configurations the formatter should run under.", 
            Separator = ',')]
        public IEnumerable<string> PreprocessorConfigurations { get; set; }

        [Option(
            "copyright", 
            HelpText = "Specifies file containing copyright header.")]
        public string CopyrightHeaderFile { get; set;  }

        [Option(
            "enable", 
            HelpText = "Comma-separated list of rules to enable", 
            Separator = ',')]
        public IEnumerable<string> EnabledRules { get; set; }

        [Option(
            "disable", 
            HelpText = "Comma-separated list of rules to disable", 
            Separator = ',')]
        public IEnumerable<string> DisabledRules { get; set; }

        [Option(
            'v', "verbose", 
            HelpText = "Verbose output.")]
        public bool Verbose { get; set; }

        [Option(
            "define-dotnet_formatter", 
            HelpText = "Define DOTNET_FORMATTER in order to allow #if !DOTNET_FORMATTER constructs in code (to opt out of reformatting).")]
        public bool DefineDotNetFormatter { get; set; }
        
        [Option(
            "use-analyzers",
            HelpText = "TEMPORARY: invoke built-in analyzers rather than rules to perform reformatting.")]
        public bool UseAnalyzers { get; set; }

        private ImmutableArray<string> _copyrightHeaderText;
        public ImmutableArray<string> CopyrightHeaderText
        {
            get
            {
                if (_copyrightHeaderText == null)
                {
                    _copyrightHeaderText = InitializeCopyrightHeaderText(CopyrightHeaderFile);
                }
                return _copyrightHeaderText;
            }
            internal set
            {
                _copyrightHeaderText = value;
            }
        }

        private static ImmutableArray<string> InitializeCopyrightHeaderText(string copyrightHeaderFile)
        {
            ImmutableArray<string> copyrightHeaderText = FormattingDefaults.DefaultCopyrightHeader;

            if (!String.IsNullOrEmpty(copyrightHeaderFile))
            {
                copyrightHeaderText = ImmutableArray.CreateRange(File.ReadAllLines(copyrightHeaderFile));
            }

            return copyrightHeaderText;
        }

        private ImmutableDictionary<string, bool> _ruleMap;
        public ImmutableDictionary<string, bool> RuleMap
        {
            get
            {
                return _ruleMap ?? BuildRuleMapFromOptions();
            }
        }

        private ImmutableDictionary<string, bool> BuildRuleMapFromOptions()
        {
            _ruleMap = ImmutableDictionary<string, bool>.Empty;
            if (EnabledRules != null)
            {
                foreach (string rule in EnabledRules)
                {
                    _ruleMap = _ruleMap.SetItem(rule, true);
                }
            }

            if (DisabledRules != null)
            {
                foreach (string rule in DisabledRules)
                {
                    _ruleMap = _ruleMap.SetItem(rule, false);
                }
            }
            return _ruleMap;
        }
    }
}
