using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    /// <summary>
    /// This is a MEF importable type which contains all of the options for formatting
    /// </summary>
    [Export(typeof(Options))]
    internal sealed class Options
    {
        private static readonly string[] s_defaultCopyrightHeader =
        {
            "// Copyright (c) Microsoft. All rights reserved.",
            "// Licensed under the MIT license. See LICENSE file in the project root for full license information."
        };

        internal ImmutableArray<string> CopyrightHeader { get; set; }
        internal ImmutableArray<string[]> PreprocessorConfigurations { get; set; }
        internal IFormatLogger FormatLogger { get; set; }

        [ImportingConstructor]
        internal Options()
        {
            CopyrightHeader = ImmutableArray.Create(s_defaultCopyrightHeader);
            PreprocessorConfigurations = ImmutableArray<string[]>.Empty;
            FormatLogger = new ConsoleFormatLogger();
        }
    }
}
