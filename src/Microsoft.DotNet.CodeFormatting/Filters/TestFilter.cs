using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Filters
{
    //  Useful to restrict processing to a single file when testing code
    [Export(typeof(IFormattingFilter))]
    [PartMetadata(RuleTypeConstants.PartMetadataKey, "Test")]
    internal sealed class TestFilter : IFormattingFilter
    {
        public Task<bool> ShouldBeProcessedAsync(Document document)
        {
            //if (document.FilePath.EndsWith("ActivationEventOrderingTests.cs"))
            if (document.FilePath.EndsWith("PartBuilderOfTTests.cs"))
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
