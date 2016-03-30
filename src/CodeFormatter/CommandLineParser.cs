using Microsoft.CodeAnalysis;
using Microsoft.DotNet.CodeFormatting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        ShowHelp
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

        public static readonly CommandLineOptions ShowHelp = new CommandLineOptions(
            Operation.ShowHelp,
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

    public sealed class CommandLineParseResult
    {
        private readonly CommandLineOptions _options;
        private readonly string _error;

        public bool IsSuccess
        {
            get { return _options != null; }
        }

        public bool IsError
        {
            get { return !IsSuccess; }
        }

        public CommandLineOptions Options
        {
            get
            {
                Debug.Assert(IsSuccess);
                return _options;
            }
        }

        public string Error
        {
            get
            {
                Debug.Assert(IsError);
                return _error;
            }
        }

        private CommandLineParseResult(CommandLineOptions options = null, string error = null)
        {
            _options = options;
            _error = error;
        }

        public static CommandLineParseResult CreateSuccess(CommandLineOptions options)
        {
            return new CommandLineParseResult(options: options);
        }

        public static CommandLineParseResult CreateError(string error)
        {
            return new CommandLineParseResult(error: error);
        }
    }

    public static class CommandLineParser
    {
        private const string FileSwitch = "/file:";
        private const string ConfigSwitch = "/c:";
        private const string CopyrightWithFileSwitch = "/copyright:";
        private const string LanguageSwitch = "/lang:";
        private const string RuleEnabledSwitch1 = "/rule+:";
        private const string RuleEnabledSwitch2 = "/rule:";
        private const string RuleDisabledSwitch = "/rule-:";
        private const string Usage =
@"CodeFormatter [/file:<filename>] [/lang:<language>] [/c:<config>[,<config>...]>]
    [/copyright(+|-):[<file>]] [/tables] [/nounicode] 
    [/rule(+|-):rule1,rule2,...]  [/verbose]
    <project, solution or response file>

    /file           - Only apply changes to files with specified name
    /lang           - Specifies the language to use when a responsefile is
                      specified. i.e. 'C#', 'Visual Basic', ... (default: 'C#')
    /c              - Additional preprocessor configurations the formatter
                      should run under.
    /copyright(+|-) - Enables or disables (default) updating the copyright 
                      header in files, optionally specifying a file 
                      containing a custom copyright header.                   
    /nocopyright    - Do not update the copyright message.
    /tables         - Let tables opt out of formatting by defining
                      DOTNET_FORMATTER
    /nounicode      - Do not convert unicode strings to escape sequences
    /rule(+|-)      - Enable (default) or disable the specified rule
    /rules          - List the available rules
    /verbose        - Verbose output
    /help           - Displays this usage message (short form: /?)
";

        public static void PrintUsage()
        {
            Console.WriteLine(Usage);
        }

        public static bool TryParse(string[] args, out CommandLineOptions options)
        {
            var result = Parse(args);
            options = result.IsSuccess ? result.Options : null;
            return result.IsSuccess;
        }

        public static CommandLineParseResult Parse(string[] args)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var comparison = StringComparison.OrdinalIgnoreCase;
            var formatTargets = new List<string>();
            var fileNames = new List<string>();
            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            var copyrightHeader = ImmutableArray<string>.Empty;
            var ruleMap = ImmutableDictionary<string, bool>.Empty;
            var language = LanguageNames.CSharp;
            var allowTables = false;
            var verbose = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith(ConfigSwitch, comparison))
                {
                    var all = arg.Substring(ConfigSwitch.Length);
                    var configs = all.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    configBuilder.Add(configs);
                }
                else if (comparer.Equals(arg, "/copyright+") || comparer.Equals(arg, "/copyright"))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.CopyrightRuleName, true);
                    copyrightHeader = FormattingDefaults.DefaultCopyrightHeader;
                }
                else if (arg.StartsWith(CopyrightWithFileSwitch, comparison))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.CopyrightRuleName, true);

                    var fileName = arg.Substring(CopyrightWithFileSwitch.Length);
                    try
                    {
                        copyrightHeader = ImmutableArray.CreateRange(File.ReadAllLines(fileName));
                    }
                    catch (Exception ex)
                    {
                        string error = string.Format("Could not read {0}{1}{2}",
                           fileName,
                           Environment.NewLine,
                           ex.Message);
                        return CommandLineParseResult.CreateError(error);
                    }
                }
                else if (comparer.Equals(arg, "/copyright-") || comparer.Equals(arg, "/nocopyright")) 
                {   // We still check /nocopyright for backwards compat

                    ruleMap = ruleMap.SetItem(FormattingDefaults.CopyrightRuleName, false);
                }
                else if (arg.StartsWith(LanguageSwitch, comparison))
                {
                    language = arg.Substring(LanguageSwitch.Length);
                }
                else if (comparer.Equals(arg, "/nounicode"))
                {
                    ruleMap = ruleMap.SetItem(FormattingDefaults.UnicodeLiteralsRuleName, false);
                }
                else if (comparer.Equals(arg, "/verbose"))
                {
                    verbose = true;
                }
                else if (arg.StartsWith(FileSwitch, comparison))
                {
                    fileNames.Add(arg.Substring(FileSwitch.Length));
                }
                else if (arg.StartsWith(RuleEnabledSwitch1, comparison))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleEnabledSwitch1.Length), enabled: true);
                }
                else if (arg.StartsWith(RuleEnabledSwitch2, comparison))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleEnabledSwitch2.Length), enabled: true);
                }
                else if (arg.StartsWith(RuleDisabledSwitch, comparison))
                {
                    UpdateRuleMap(ref ruleMap, arg.Substring(RuleDisabledSwitch.Length), enabled: false);
                }
                else if (comparer.Equals(arg, "/tables"))
                {
                    allowTables = true;
                }
                else if (comparer.Equals(arg, "/rules"))
                {
                    return CommandLineParseResult.CreateSuccess(CommandLineOptions.ListRules);
                }
                else if (comparer.Equals(arg, "/?") || comparer.Equals(arg, "/help"))
                {
                    return CommandLineParseResult.CreateSuccess(CommandLineOptions.ShowHelp);
                }
                else if (arg.StartsWith("/", comparison))
                {
                    return CommandLineParseResult.CreateError($"Unrecognized option \"{arg}\"");
                }
                else
                {
                    formatTargets.Add(arg);
                }
            }

            if (formatTargets.Count == 0)
            {
                return CommandLineParseResult.CreateError("Must specify at least one project / solution / rsp to format");
            }

            var options = new CommandLineOptions(
                Operation.Format,
                configBuilder.ToImmutableArray(),
                copyrightHeader,
                ruleMap,
                formatTargets.ToImmutableArray(),
                fileNames.ToImmutableArray(),
                language,
                allowTables,
                verbose);
            return CommandLineParseResult.CreateSuccess(options);
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
