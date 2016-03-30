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

        private CommandLineOptions FailToParse(params string[] args)
        {
            CommandLineOptions options;
            Assert.False(CommandLineParser.TryParse(args, out options));
            Assert.Null(options);

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
        public void CopyrightDisable()
        {
            var options = Parse("/copyright-", "test.csproj");
            Assert.False(options.RuleMap[FormattingDefaults.CopyrightRuleName]);
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
        public void Help()
        {
            var options = Parse("/help");
            Assert.Equal(options.Operation, Operation.ShowHelp);
        }

        [Fact]
        public void HelpShortForm()
        {
            var options = Parse("/?");
            Assert.Equal(options.Operation, Operation.ShowHelp);
        }

        [Fact]
        public void HelpWithOtherwiseValidArguments()
        {
            var options = Parse("test.csproj", "/nocopyright", "/help");
            Assert.Equal(options.Operation, Operation.ShowHelp);
        }

        [Fact]
        public void CopyrightEnable1()
        {
            var options = Parse("/copyright+", "test.csproj");
            Assert.True(options.RuleMap[FormattingDefaults.CopyrightRuleName]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void CopyrightEnable2()
        {
            var options = Parse("/copyright", "test.csproj");
            Assert.True(options.RuleMap[FormattingDefaults.CopyrightRuleName]);
            Assert.Equal(new[] { "test.csproj" }, options.FormatTargets);
        }

        [Fact]
        public void SingleUnrecognizedOption()
        {
            FailToParse("/unrecognized");
        }

        [Fact]
        public void UnrecognizedOptionWithFormatTarget()
        {
            FailToParse("test.csproj", "/unrecognized");
        }

        [Fact]
        public void UnrecognizedOptionWithOtherwiseValidArguments()
        {
            FailToParse("test.csproj", "/nocopyright", "/unrecognized");
        }
    }
}
