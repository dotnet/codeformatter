using Microsoft.CodeAnalysis;
using Microsoft.DotNet.CodeFormatting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeFormatter
{
    public enum Operation
    {
        Format,
        ListRules,
    }

    public sealed class CommandLineOptions
    {
        public static readonly CommandLineOptions ListRules = new CommandLineOptions(
            Operation.ListRules,
            ImmutableArray<string[]>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableDictionary<string, bool>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null,
            allowTables: false,
            verbose: false);

        public readonly Operation Operation;
        public readonly ImmutableArray<string[]> PreprocessorConfigurations;
        public readonly ImmutableArray<string> CopyrightHeader;
        public readonly ImmutableDictionary<string, bool> RuleMap;
        public readonly ImmutableArray<string> FormatTargets;
        public readonly ImmutableArray<string> FileNames;
        public readonly string Language;
        public readonly bool AllowTables;
        public readonly bool Verbose;

        public CommandLineOptions(
            Operation operation,
            ImmutableArray<string[]> preprocessorConfigurations,
            ImmutableArray<string> copyrightHeader,
            ImmutableDictionary<string, bool> ruleMap,
            ImmutableArray<string> formatTargets,
            ImmutableArray<string> fileNames,
            string language,
            bool allowTables,
            bool verbose)
        {
            Operation = operation;
            PreprocessorConfigurations = preprocessorConfigurations;
            CopyrightHeader = copyrightHeader;
            RuleMap = ruleMap;
            FileNames = fileNames;
            FormatTargets = formatTargets;
            Language = language;
            AllowTables = allowTables;
            Verbose = verbose;
        }
    }

    public static class CommandLineParser
    {
        private const string FileSwitch = "/file:";
        private const string ConfigSwitch = "/c:";
        private const string CopyrightSwitch = "/copyright:";
        private const string LanguageSwitch = "/lang:";
        private const string RuleEnabledSwitch1 = "/rule+:";
        private const string RuleEnabledSwitch2 = "/rule:";
        private const string RuleDisabledSwitch = "/rule-:";

        public static void PrintUsage()
        {
            Console.WriteLine(
@"CodeFormatter [/file:<filename>] [/lang:<language>] [/c:<config>[,<config>...]>]
    [/copyright:<file> | /nocopyright] [/tables] [/nounicode] 
    [/rule(+|-):rule1,rule2,...]  [/verbose]
    <project, solution or response file>

    /file        - Only apply changes to files with specified name
    /lang        - Specifies the language to use when a responsefile is
                   specified. i.e. 'C#', 'Visual Basic', ... (default: 'C#')
    /c           - Additional preprocessor configurations the formatter
                   should run under.
    /copyright   - Specifies file containing copyright header.
                   Use ConvertTests to convert MSTest tests to xUnit.
    /nocopyright - Do not update the copyright message.
    /tables      - Let tables opt out of formatting by defining
                   DOTNET_FORMATTER
    /nounicode   - Do not convert unicode strings to escape sequences
    /rule(+|-)   - Enable (default) or disable the specified rule
    /rules       - List the available rules
    /verbose     - Verbose output
");
        }

        public static bool TryParse(string[] args, out CommandLineOptions options)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                options = null;
                return false;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var formatTargets = new List<string>();
            var fileNames = new List<string>();
            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            var copyrightHeader = FormattingDefaults.DefaultCopyrightHeader;
            var ruleMap = ImmutableDictionary<string, bool>.Empty;
            var language = LanguageNames.CSharp;
            var allowTables = false;
            var verbose = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith(ConfigSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    var all = arg.Substring(ConfigSwitch.Length);
                    var configs = all.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    configBuilder.Add(configs);
                }
                else if (arg.StartsWith(CopyrightSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = arg.Substring(CopyrightSwitch.Length);
                    try
                    {
                        copyrightHeader = ImmutableArray.CreateRange(File.ReadAllLines(fileName));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Could not read {0}", fileName);
                        Console.Error.WriteLine(ex.Message);
                        options = null;
                        return false;
                    }
                }
                else if (arg.StartsWith(LanguageSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    language = arg.Substring(LanguageSwitch.Length);
                }
                else if (comparer.Equals(arg, "/nocopyright"))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.CopyrightRuleName, false);
                }
                else if (comparer.Equals(arg, "/nounicode"))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.UnicodeLiteralsRuleName, false);
                }
                else if (comparer.Equals(arg, "/verbose"))
                {
                    verbose = true;
                }
                else if (comparer.Equals(arg, FileSwitch))
                {
                    fileNames.Add(arg.Substring(FileSwitch.Length));
                }
                else if (comparer.Equals(arg, RuleEnabledSwitch1))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleEnabledSwitch1.Length), enabled: true);
                }
                else if (comparer.Equals(arg, RuleEnabledSwitch2))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleEnabledSwitch2.Length), enabled: true);
                }
                else if (comparer.Equals(arg, RuleDisabledSwitch))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleDisabledSwitch.Length), enabled: false);
                }
                else if (comparer.Equals(arg, "/tables"))
                {
                    allowTables = true;
                }
                else if (comparer.Equals(arg, "/rules"))
                {
                    options = CommandLineOptions.ListRules;
                    return true;
                }
                else
                {
                    formatTargets.Add(arg);
                }
            }

            options = new CommandLineOptions(
                Operation.Format,
                configBuilder.ToImmutableArray(),
                copyrightHeader,
                ruleMap,
                formatTargets.ToImmutableArray(),
                fileNames.ToImmutableArray(),
                language,
                allowTables,
                verbose);
            return true;
        }

        private static void UpdateRuleMap(ref ImmutableDictionary<string, bool> ruleMap, string data, bool enabled)
        {
            foreach (var current in data.Split(','))
            {
                ruleMap = ruleMap.SetItem(current, enabled);
            }
        }
    }
}
