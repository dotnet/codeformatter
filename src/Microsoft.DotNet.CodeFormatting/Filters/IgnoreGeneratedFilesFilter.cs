// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    internal sealed class IgnoreGeneratedFilesFilter : IFormattingFilter
    {
        public bool ShouldBeProcessed(Document document)
        {
            if (document.FilePath == null)
            {
                return true;
            }

            if (document.FilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
                document.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase) ||
                document.FilePath.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase) ||
                document.FilePath.EndsWith(".Generated.vb", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}