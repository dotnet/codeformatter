// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Collections.Generic;

using CodeFormatter;

using CommandLine;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class FormatOptionsParsingTests
    {
        private FormatOptions Parse(out int result, params string[] args)
        {
            result = 1;
            FormatOptions options = null;

            var parser = new Parser(settings =>
            {
                // CommandLine library help output is not thread-safe. We
                // disable console output entirely by providing a null writer
                settings.HelpWriter = null;
            });
                            
            // We are only interested in formatting options, but we must provide
            // at least two verbs to ParseArguments in order to realize appropriate
            // behavior around parsing the verb name...
            result = parser.ParseArguments<ExportOptions, FormatOptions>(args)
                .MapResult(
                (FormatOptions parsedOptions) => { options = parsedOptions; return 0; },
                errs => ReportErrors(errs));

            return options;
        }

        private int ReportErrors(IEnumerable<Error> errs)
        {
            return 1;
        }

        [Fact]
        public void AllRulesEnabled()
        {
            int result;

            // CodeFormatter format test.csproj --options-file-path AllEnabled.formatconfig
            var options = Parse(out result, "format", "test.csproj", "--options-file-path", "AllEnabled.formatconfig");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.Equal(12, options.RuleMap.Count);
            Assert.Equal(new[] { "test.csproj" }, options.Targets);

            foreach (bool enabledSetting in options.RuleMap.Values)
            {
                Assert.True(enabledSetting);
            }
        }

        [Fact]
        public void AllRulesDisabled()
        {
            int result;

            // CodeFormatter format test.csproj --options-file-path AllDisabled.formatconfig
            var options = Parse(out result, "format", "test.csproj", "--options-file-path", "AllDisabled.formatconfig");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.Equal(12, options.RuleMap.Count);
            Assert.Equal(new[] { "test.csproj" }, options.Targets);

            foreach(bool enabledSetting in options.RuleMap.Values)
            {
                Assert.False(enabledSetting);
            }
        }

        [Fact]
        public void TargetOmitted()
        {
            int result;

            // CodeFormatter format --enable enabledRule
            var options = Parse(out result, "format", "--enable", "enabledRule");

            Assert.Equal(1, result);
            Assert.Null(options);
        }

        [Fact]
        public void MultipleTargets()
        {
            int result;

            // CodeFormatter format --enable enabledRule
            var options = Parse(out result, "format", "projectOne.csproj", "projectTwo.csproj");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.Equal(2, options.Targets.Count());
            Assert.True(options.Targets.Contains("projectOne.csproj"));
            Assert.True(options.Targets.Contains("projectTwo.csproj"));
        }

        [Fact]
        public void UseAnalyzers()
        {
            int result;

            // CodeFormatter format --target test.csproj --useanalyzers
            var options = Parse(out result, "format", "test.csproj", "--use-analyzers");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.True(options.UseAnalyzers);
        }

        [Fact]
        public void UseAnalyzersDefaultIsFalse()
        {
            int result;

            // CodeFormatter format --target test.csproj --useanalyzers
            var options = Parse(out result, "format", "test.csproj");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.False(options.UseAnalyzers);
        }
    }
}
