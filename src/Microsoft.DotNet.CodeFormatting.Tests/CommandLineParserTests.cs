// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CodeFormatter;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class CommandLineParserTests
    {
        private CommandLineOptions Parse(params string[] args)
        {
            CommandLineOptions options;
            Assert.True(CommandLineParser.TryParse(args, out options));
            return options;
        }

        [Fact]
        public void Rules()
        {
            var options = Parse("/rules");
            Assert.Equal(Operation.ListRules, options.Operation);
        }

        [Fact]
        public void Rules1()
        {
            var options = Parse("/rule:r1", "test.csproj");
            Assert.True(options.RuleMap["r1"]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void Rules2()
        {
            var options = Parse("/rule+:r1", "test.csproj");
            Assert.True(options.RuleMap["r1"]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void Rules3()
        {
            var options = Parse("/rule-:r1", "test.csproj");
            Assert.False(options.RuleMap["r1"]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void Rules4()
        {
            var options = Parse("/rule:r1,r2,r3", "test.csproj");
            Assert.True(options.RuleMap["r1"]);
            Assert.True(options.RuleMap["r2"]);
            Assert.True(options.RuleMap["r3"]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void Rules5()
        {
            var options = Parse("/rule-:r1,r2,r3", "test.csproj");
            Assert.False(options.RuleMap["r1"]);
            Assert.False(options.RuleMap["r2"]);
            Assert.False(options.RuleMap["r3"]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void NeedAtLeastOneTarget()
        {
            CommandLineOptions options;
            Assert.False(CommandLineParser.TryParse(new[] { "/rule:foo" }, out options));
        }

        [Fact]
        public void NoUnicode()
        {
            var options = Parse("/nounicode", "test.csproj");
            Assert.False(options.RuleMap[FormattingDefaults.UnicodeLiteralsRuleName]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void NoCopyright()
        {
            var options = Parse("/nocopyright", "test.csproj");
            Assert.False(options.RuleMap[FormattingDefaults.CopyrightRuleName]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void UseAnalyzers()
        {
            var options = Parse("test.csproj", "/useanalyzers");
            Assert.True(options.UseAnalyzers);
        }

        [Fact]
        public void DontUseAnalyzers()
        {
            var options = Parse("test.csproj");
            Assert.False(options.UseAnalyzers);
        }
    }
}
