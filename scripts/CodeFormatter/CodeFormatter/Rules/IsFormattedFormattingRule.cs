using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using CodeFormatter.Engine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeFormatter.Rules
{
    [Export(typeof(IFormattingRule))]
    internal sealed class IsFormattedFormattingRule : IFormattingRule
    {
        public Task<Document> ProcessAsync(CancellationToken cancellationToken, Document document)
        {
            return Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        }
    }
}