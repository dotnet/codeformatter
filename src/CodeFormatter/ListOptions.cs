// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CommandLine;

namespace CodeFormatter
{
    [Verb("list", HelpText = "List available built-in rules and analyzers.")]
    internal class ListOptions
    {
        [Option('r', "rules", HelpText = "List available built-in rules")]
        public bool Rules { get; set; }

        [Option('a', "analyzers", HelpText = "List available built-in analyzers")]
        public bool Analyzers { get; set; }
    }
}
