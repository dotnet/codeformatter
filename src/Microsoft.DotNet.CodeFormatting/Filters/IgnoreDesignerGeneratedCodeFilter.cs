// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    [Export(typeof(IFormattingFilter))]
    internal sealed class IgnoreDesignerGeneratedCodeFilter : IFormattingFilter
    {
        public Task<bool> ShouldBeProcessedAsync(Document document)
        {
            var isDesignerGenerated = document.FilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(!isDesignerGenerated);
        }
    }
}