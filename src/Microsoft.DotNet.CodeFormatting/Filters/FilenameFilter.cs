// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    [Export(typeof(IFormattingFilter))]
    internal sealed class FilenameFilter : IFormattingFilter
    {
        private readonly Options _options;

        [ImportingConstructor]
        public FilenameFilter(Options options)
        {
            _options = options;
        }

        public bool ShouldBeProcessed(Document document)
        {
            var fileNames = _options.FileNames;
            if (fileNames.IsDefaultOrEmpty)
            {
                return true;
            }

            string docFilename = Path.GetFileName(document.FilePath);
            foreach (var filename in fileNames)
            {
                if (filename.Equals(docFilename, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
