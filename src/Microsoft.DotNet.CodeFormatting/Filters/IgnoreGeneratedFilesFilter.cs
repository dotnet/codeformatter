// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    [Export(typeof(IFormattingFilter))]
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
