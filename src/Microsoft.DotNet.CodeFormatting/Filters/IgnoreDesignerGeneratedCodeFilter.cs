// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    [Export(typeof(IFormattingFilter))]
    internal sealed class IgnoreDesignerGeneratedCodeFilter : IFormattingFilter
    {
        public bool ShouldBeProcessed(Document document)
        {
            if (document.FilePath == null)
            {
                return true;
            }

            var isDesignerGenerated = document.FilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
            return !isDesignerGenerated;
        }
    }
}