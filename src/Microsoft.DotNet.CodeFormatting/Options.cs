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
        internal ImmutableArray<string> CopyrightHeader { get; set; }
        internal ImmutableArray<string[]> PreprocessorConfigurations { get; set; }

        /// <summary>
        /// When non-empty the formatter will only process files with the specified name.
        /// </summary>
        internal ImmutableArray<string> FileNames { get; set; }

        internal IFormatLogger FormatLogger { get; set; }

        internal bool ConvertUnicodeCharacters { get; set; }

        [ImportingConstructor]
        internal Options()
        {
            CopyrightHeader = FormattingConstants.DefaultCopyrightHeader;
            FileNames = ImmutableArray<string>.Empty;
            PreprocessorConfigurations = ImmutableArray<string[]>.Empty;
            FormatLogger = new ConsoleFormatLogger();
            ConvertUnicodeCharacters = true;
        }
    }
}
