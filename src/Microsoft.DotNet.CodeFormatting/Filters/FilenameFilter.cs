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
    internal sealed class FilenameFilter : IFormattingFilter
    {
        private IEnumerable<string> _filenames;

        public FilenameFilter(IEnumerable<string> filenames)
        {
            _filenames = filenames;
        }

        public Task<bool> ShouldBeProcessedAsync(Document document)
        {
            if (!_filenames.Any())
            {
                return Task.FromResult(true);
            }

            string docFilename = Path.GetFileName(document.FilePath);

            foreach (var filename in _filenames)
            {
                if (filename.Equals(docFilename, StringComparison.InvariantCultureIgnoreCase))
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
    }
}
