// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.IO;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    [Export(typeof(IFormattingFilter))]
    internal sealed class UsableFileFilter : IFormattingFilter
    {
        private readonly Options _options;

        [ImportingConstructor]
        public UsableFileFilter(Options options)
        {
            _options = options;
        }

        public bool ShouldBeProcessed(Document document)
        {
            if (document.FilePath == null)
            {
                return true;
            }

            var fileInfo = new FileInfo(document.FilePath);
            if (!fileInfo.Exists || fileInfo.IsReadOnly)
            {
                _options.FormatLogger.WriteLine("warning: skipping document '{0}' because it {1}.",
                    document.FilePath,
                    fileInfo.IsReadOnly ? "is read-only" : "does not exist");
                return false;
            }

            return true;
        }
    }
}
