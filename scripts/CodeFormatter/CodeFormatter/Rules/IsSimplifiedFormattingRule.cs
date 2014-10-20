using System;
using System.Threading;
using System.Threading.Tasks;

using CodeFormatter.Engine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Simplification;

namespace CodeFormatter.Rules
{
    [ExportFormattingRule(Int32.MaxValue / 2)]
    internal sealed class IsSimplifiedFormattingRule : IFormattingRule
    {
        public Task<Document> ProcessAsync(CancellationToken cancellationToken, Document document)
        {
            return Simplifier.ReduceAsync(document, cancellationToken: cancellationToken);
        }
    }
}