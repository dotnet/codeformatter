// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
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
                .Return(
                (FormatOptions parsedOptions) => { options = parsedOptions; return 0; },
                errs => ReportErrors(errs));

            return options;
        }

        private int ReportErrors(IEnumerable<Error> errs)
        {
            return 1;
        }

        [Fact]
        public void EnableRule()
        {
            int result;

            // CodeFormatter format test.csproj --enable enabledRule 
            var options = Parse(out result, "format", "test.csproj", "--enable", "enabledRule");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.True(options.RuleMap["enabledRule"]);
            Assert.Equal(1, options.RuleMap.Count);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }


        [Fact]
        public void DisableRule()
        {
            int result;

            // CodeFormatter format test.csproj --disable disabledRule
            var options = Parse(out result, "format", "test.csproj", "--disable", "disabledRule");

            Assert.Equal(0, result);
            Assert.NotNull(options);
            Assert.False(options.RuleMap["disabledRule"]);
            Assert.Equal(1, options.RuleMap.Count);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void EnableAndDisableMultipleRules()
        {
            int result;

            // CodeFormatter format --enable e1,e2 --disable d1,d2,d3 test.csproj
            var options = Parse(out result, "format", "test.csproj", "--enable", "e1,e2,e3", "--disable", "d1,d2,d3");

            Assert.Equal(0, result);
            Assert.NotNull(options);

            Assert.True(options.RuleMap["e1"]);
            Assert.True(options.RuleMap["e2"]);
            Assert.True(options.RuleMap["e3"]);

            Assert.False(options.RuleMap["d1"]);
            Assert.False(options.RuleMap["d2"]);
            Assert.False(options.RuleMap["d3"]);

            Assert.True(options.RuleMap.Count == 6);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
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
            Assert.Equal(2, options.FormatTargets.Count());
            Assert.True(options.FormatTargets.Contains("projectOne.csproj"));
            Assert.True(options.FormatTargets.Contains("projectTwo.csproj"));
        }

        [Fact]
        public void UseAnalyzers()
        {
            int result;

            // CodeFormatter format --target test.csproj --useanalyzers
            var options = Parse(out result, "format", "test.csproj", "--useanalyzers");

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
