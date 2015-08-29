// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CommandLine;

namespace CodeFormatter
{
    [Verb("options", HelpText = "Export rule options to an XML file that can be edited and used to configure subsequent formattings.")]
    internal class ExportOptions
    {
        [Value(0, HelpText = "Output path for exported formatting options", Required = true)]
        public string OutputPath { get; set; }
    }
}
