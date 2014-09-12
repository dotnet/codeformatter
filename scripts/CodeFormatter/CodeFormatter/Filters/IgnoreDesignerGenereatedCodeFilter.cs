using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using CodeFormatter.Engine;

using Microsoft.CodeAnalysis;

namespace CodeFormatter.Filters
{
    [Export(typeof(IFormattingFilter))]
    internal sealed class IgnoreDesignerGenereatedCodeFilter : IFormattingFilter
    {
        public Task<bool> ShouldBeProcessedAsync(Document document)
        {
            var isDesignerGenerated = document.FilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(!isDesignerGenerated);
        }
    }
}