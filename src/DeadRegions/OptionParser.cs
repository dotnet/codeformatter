// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeadRegions
{
    internal class OptionParser
    {
        private static readonly Regex s_exeNameRegex = new Regex(@"^((""[^""]+"")|\S+)\s*", RegexOptions.ExplicitCapture);
        private static readonly Regex s_optionRegex = new Regex(@"\G[/-](?<name>[^:^=^\s]+)([:=]|\s+)?", RegexOptions.ExplicitCapture);
        private static readonly Regex s_valueRegex = new Regex(@"\G\s*((""(?<value>[^""]+)"")|(?<value>[^""^/^-^\s]+))\s*", RegexOptions.ExplicitCapture);
        private static readonly Regex s_responseFileRegex = new Regex(@"\G@((""(?<file>[^""]+)"")|(?<file>\S+))\s*", RegexOptions.ExplicitCapture);

        private Dictionary<string, Option> _options = new Dictionary<string, Option>(StringComparer.OrdinalIgnoreCase);

        public string Usage
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("OPTIONS");

                foreach (var option in _options.Values)
                {
                    sb.AppendLine(option.Usage);
                }

                sb.AppendLine("  @<response file>");
                sb.AppendLine("    Use the contents of the specified file as additional command line input");
                sb.AppendLine("    [multiple specifications allowed]");

                return sb.ToString();
            }
        }

        public void Add(string name, Action<string> action, string parameterUsage = null, string description = null, bool allowMultiple = false)
        {
            _options.Add(name, new Option(name, action,
                requiresValue: true,
                parameterUsage: parameterUsage,
                description: description,
                allowMultiple: allowMultiple));
        }

        public void Add(string name, Action action, string description = null)
        {
            _options.Add(name, new Option(name, action,
                requiresValue: false,
                parameterUsage: null,
                description: description,
                allowMultiple: false));
        }

        public ImmutableArray<string> Parse(string commandLine, bool firstArgumentIsPathToExe = true)
        {
            int index = 0;
            var unprocessedValues = ImmutableArray.CreateBuilder<string>();

            if (firstArgumentIsPathToExe)
            {
                var exeName = s_exeNameRegex.Match(commandLine);
                Debug.Assert(exeName.Success);
                index = exeName.Length;
            }

            while (index < commandLine.Length)
            {
                var optionMatch = s_optionRegex.Match(commandLine, index);
                if (optionMatch.Success)
                {
                    index += optionMatch.Length;
                    string optionName = optionMatch.Groups["name"].Value;

                    Option option;
                    if (_options.TryGetValue(optionName, out option))
                    {
                        if (option.RequiresValue)
                        {
                            var valueMatch = s_valueRegex.Match(commandLine, index);
                            if (valueMatch.Success)
                            {
                                index += valueMatch.Length;
                            }
                            else
                            {
                                throw new OptionParseException("Missing value for option: " + optionName);
                            }

                            option.Action.DynamicInvoke(valueMatch.Groups["value"].Value);
                        }
                        else
                        {
                            option.Action.DynamicInvoke();
                        }
                    }
                    else
                    {
                        throw new OptionParseException("Unknown option: " + optionName);
                    }
                    continue;
                }

                var responseFileMatch = s_responseFileRegex.Match(commandLine, index);
                if (responseFileMatch.Success)
                {
                    index += responseFileMatch.Length;

                    string filePath = responseFileMatch.Groups["file"].Value;
                    string text;

                    try
                    {
                        text = File.ReadAllText(filePath);
                    }
                    catch (Exception)
                    {
                        throw new OptionParseException("Failed to read response file: " + filePath);
                    }

                    unprocessedValues.AddRange(Parse(text, firstArgumentIsPathToExe: false));
                    continue;
                }

                var unprocessedValueMatch = s_valueRegex.Match(commandLine, index);
                if (unprocessedValueMatch.Success)
                {
                    index += unprocessedValueMatch.Length;
                    unprocessedValues.Add(unprocessedValueMatch.Groups["value"].Value);
                    continue;
                }
            }

            return unprocessedValues.ToImmutable();
        }

        private class Option
        {
            public readonly string Name;
            public readonly Delegate Action;
            public readonly bool RequiresValue;
            public readonly bool AllowMultiple;

            public readonly string _description;
            private readonly string _parameterUsage;

            public Option(string name, Delegate action, bool requiresValue, string parameterUsage, string description, bool allowMultiple)
            {
                Name = name;
                Action = action;
                RequiresValue = requiresValue;
                AllowMultiple = allowMultiple;

                _parameterUsage = parameterUsage;
                _description = description;
            }

            public string Usage
            {
                get
                {
                    var sb = new StringBuilder();
                    sb.Append("  /");
                    sb.Append(Name);
                    sb.Append(' ');
                    if (_parameterUsage != null)
                    {
                        sb.Append(_parameterUsage);
                    }
                    sb.Append(Environment.NewLine);
                    if (_description != null)
                    {
                        sb.Append(WrapStringAtColumn(80, "    ", _description));
                    }
                    if (AllowMultiple)
                    {
                        sb.AppendLine("    [multiple specifications allowed]");
                    }
                    return sb.ToString();
                }
            }

            private static string WrapStringAtColumn(int column, string linePrefix, string s)
            {
                column -= linePrefix.Length;

                var sb = new StringBuilder();
                do
                {
                    sb.Append(linePrefix);

                    int i;
                    for (i = Math.Min(column, s.Length); i > 0 && i < s.Length && s[i - 1] != ' '; --i) ;

                    string segment = s.Substring(0, i);
                    sb.AppendLine(segment);
                    s = s.Substring(segment.Length);
                }
                while (s.Length > 0);

                return sb.ToString();
            }
        }
    }

    internal class OptionParseException : Exception
    {
        public OptionParseException(string message) : base(message)
        {
        }
    }
}
