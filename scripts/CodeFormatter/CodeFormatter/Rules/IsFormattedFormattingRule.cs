using System;
using System.Threading;
using System.Threading.Tasks;

using CodeFormatter.Engine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeFormatter.Rules
{
    [ExportFormattingRule(Int32.MaxValue)]
    internal sealed class IsFormattedFormattingRule : IFormattingRule
    {
        public Task<Document> ProcessAsync(CancellationToken cancellationToken, Document document)
        {
            return Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        }
    }
}