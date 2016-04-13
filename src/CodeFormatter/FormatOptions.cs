// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;

using CommandLine;

using Microsoft.CodeAnalysis.Options;
using Microsoft.DotNet.CodeFormatter.Analyzers;
using Microsoft.DotNet.CodeFormatting;

namespace CodeFormatter
{
    internal class CommandLineOptions
    {
        [Value(
            0,
            HelpText = "Project, solution, text file with target paths, or response file to drive code formatting.",
            Required = true)]
        public IEnumerable<string> Targets { get; set; }

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
        public string CopyrightHeaderFile { get; set; }

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

        [Option(
            "analyzers",
            HelpText = "A path to an analyzer assembly or a file containing a newline separated list of analyzer assemblies to be run against the target source.")]
        public string TargetAnalyzers { get; set; }

        [Option(
            "log-output-path",
            HelpText = "Path to a file where analysis or format results will be logged.")]
        public string LogOutputPath { get; set; }

        public virtual bool ApplyFixes { get; }

        private ImmutableArray<string> _targetAnalyzerText;
        public ImmutableArray<string> TargetAnalyzerText
        {
            get
            {
                if (_targetAnalyzerText == null)
                {
                    _targetAnalyzerText = InitializeTargetAnalyzerText(TargetAnalyzers);
                }
                return _targetAnalyzerText;
            }
            internal set
            {
                _targetAnalyzerText = value;
            }
        }

        private static ImmutableArray<string> InitializeTargetAnalyzerText(string targetAnalyzers)
        {
            var fileType = Path.GetExtension(targetAnalyzers);

            if (StringComparer.OrdinalIgnoreCase.Equals(fileType, ".dll"))
            {
                return ImmutableArray.Create(targetAnalyzers);
            }
            else if(StringComparer.OrdinalIgnoreCase.Equals(fileType, ".txt"))
            {
                ImmutableArray<string> analyzerText = new ImmutableArray<string>();

                if (!String.IsNullOrEmpty(targetAnalyzers))
                {
                    analyzerText = ImmutableArray.CreateRange(File.ReadAllLines(targetAnalyzers));
                }
                return analyzerText;
            }

            return ImmutableArray<string>.Empty;
        }

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
            var result = new Dictionary<string, bool>();

            var propertyBag = OptionsHelper.BuildDefaultPropertyBag();
            if (!string.IsNullOrEmpty(OptionsFilePath))
            {
                propertyBag.LoadFrom(OptionsFilePath);
            }

            propertyBag = (PropertyBag)propertyBag["CodeFormatterRules.Options"];
            foreach (string key in propertyBag.Keys)
            {
                string[] tokens = key.Split('.');
                Debug.Assert(tokens.Length == 2);
                Debug.Assert(tokens[1].Equals("Enabled"));

                string rule = tokens[0];
                result[rule] = (bool)propertyBag[key];
            }

            _ruleMap = result.ToImmutableDictionary();
            return _ruleMap;
        }
    }

    [Verb("format", HelpText = "Apply code formatting rules and analyzers to specified targets.")]
    internal class FormatOptions : CommandLineOptions
    {       
        public override bool ApplyFixes { get { return true; } }
    }

    [Verb("analyze", HelpText = "Apply analyzers to specified targets but do not apply code fixes.")]
    internal class AnalyzeOptions : CommandLineOptions
    {
        public override bool ApplyFixes { get { return false; } }
    }
}