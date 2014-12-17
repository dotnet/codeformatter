// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(int.MaxValue)]
    internal sealed class HasRightEncodingFormattingRule : IFormattingRule
    {
        Encoding _desiredEncoding = new UTF8Encoding(false);
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync();
            var newText = SourceText.From(text.ToString(), _desiredEncoding);
            return document.WithText(newText);
        }
    }
}

