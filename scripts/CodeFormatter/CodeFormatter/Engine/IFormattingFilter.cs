using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CodeFormatter.Engine
{
    interface IFormattingFilter
    {
        Task<bool> ShouldBeProcessedAsync(Document document);
    }
}