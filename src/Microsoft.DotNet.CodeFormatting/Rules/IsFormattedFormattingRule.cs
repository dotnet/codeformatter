// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [Export(typeof(IFormattingRule))]
    internal sealed class IsFormattedFormattingRule : IFormattingRule
    {
        public Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            return Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        }
    }
}