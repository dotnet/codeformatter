using System.Collections.Immutable;

namespace CodeFormatter
{
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
}
