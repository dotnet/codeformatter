using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CodeFormatter.Engine
{
    internal interface IFormattingRule
    {
        Task<Document> ProcessAsync(CancellationToken cancellationToken, Document document);
    }
}