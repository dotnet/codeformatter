// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CommandLine;

namespace CodeFormatter
{
    [Verb("options", HelpText = "Export rule options to an XML file that can edited and used to configure subsequents formattings.")]
    internal class ExportOptions
    {
        [Value(0, HelpText = "List available built-in rules", Required = true)]
        public bool OutputPath { get; set; }
    }
}
