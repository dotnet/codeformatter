using CodeFormatter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
