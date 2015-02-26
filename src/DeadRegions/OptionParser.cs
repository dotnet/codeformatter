using System;
using System.Collections.Generic;
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
        private Dictionary<string, Option> _options = new Dictionary<string, Option>(StringComparer.OrdinalIgnoreCase);

        public string Usage
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("OPTIONS");

                foreach (var option in _options.Values)
                {
                    sb.Append(option.Usage);
                }

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
                requiresValue: true,
                parameterUsage: null,
                description: description,
                allowMultiple: false));
        }

        public IList<string> Parse(string commandLine)
        {
            var unprocessedValues = new List<string>();

            var exeNameRegex = new Regex(@"^((""[^""]+"")|\S+)\s*", RegexOptions.ExplicitCapture);
            var exeName = exeNameRegex.Match(commandLine);
            Debug.Assert(exeName.Success);
            int index = exeName.Length;

            var optionRegex = new Regex(@"\G[/-](?<name>[^:^=^\s]+)([:=]|\s+)", RegexOptions.ExplicitCapture);
            var valueRegex = new Regex(@"\G\s*((""(?<value>[^""]+)"")|(?<value>[^""^/^-^\s]+))\s*", RegexOptions.ExplicitCapture);
            var responseFileRegex = new Regex(@"\G@((""(?<file>[^""]+)"")|(?<file>\S+))\s*", RegexOptions.ExplicitCapture);

            while (index < commandLine.Length)
            {
                var optionMatch = optionRegex.Match(commandLine, index);
                if (optionMatch.Success)
                {
                    index += optionMatch.Length;
                    string optionName = optionMatch.Groups["name"].Value;

                    Option option;
                    if (_options.TryGetValue(optionName, out option))
                    {
                        if (option.RequiresValue)
                        {
                            var valueMatch = valueRegex.Match(commandLine, index);
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

                var responseFileMatch = responseFileRegex.Match(commandLine, index);
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

                    unprocessedValues.AddRange(Parse(text));
                    continue;
                }

                var defaultValueMatch = valueRegex.Match(commandLine, index);
                if (defaultValueMatch.Success)
                {
                    index += defaultValueMatch.Length;
                    unprocessedValues.Add(defaultValueMatch.Groups["value"].Value);
                    continue;
                }
            }

            return unprocessedValues;
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
                    sb.Append("  ");
                    sb.Append(Name);
                    sb.Append(' ');
                    if (_parameterUsage != null)
                    {
                        sb.Append(_parameterUsage);
                    }
                    sb.Append(Environment.NewLine);
                    if (_description != null)
                    {
                        sb.Append("    ");
                        sb.AppendLine(_description);
                    }
                    if (AllowMultiple)
                    {
                        sb.AppendLine("    [multiple specifications allowed]");
                    }
                    return sb.ToString();
                }
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
