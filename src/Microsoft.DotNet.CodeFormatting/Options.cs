// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

namespace Microsoft.DotNet.CodeFormatting
{
    /// <summary>
    /// This is a MEF importable type which contains all of the options for formatting
    /// </summary>
    [Export(typeof(Options))]
    internal sealed class Options
    {
        internal ImmutableArray<string> CopyrightHeader { get; set; }
        internal ImmutableArray<string[]> PreprocessorConfigurations { get; set; }

        /// <summary>
        /// When non-empty the formatter will only process files with the specified name.
        /// </summary>
        internal ImmutableArray<string> FileNames { get; set; }

        internal IFormatLogger FormatLogger { get; set; }

        [ImportingConstructor]
        public Options()
        {
            CopyrightHeader = FormattingDefaults.DefaultCopyrightHeader;
            FileNames = ImmutableArray<string>.Empty;
            PreprocessorConfigurations = ImmutableArray<string[]>.Empty;
            FormatLogger = new ConsoleFormatLogger();
        }
    }
}
