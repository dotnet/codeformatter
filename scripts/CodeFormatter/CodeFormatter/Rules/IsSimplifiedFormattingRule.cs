using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using CodeFormatter.Engine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Simplification;

namespace CodeFormatter.Rules
{
    [Export(typeof (IFormattingRule))]
    internal sealed class IsSimplifiedFormattingRule : IFormattingRule
    {
        public Task<Document> ProcessAsync(CancellationToken cancellationToken, Document document)
        {
            return Simplifier.ReduceAsync(document, cancellationToken: cancellationToken);
        }
    }
}