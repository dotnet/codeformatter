using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace CodeFormatter.Engine
{
    internal interface IFormattingEngine
    {
        Task RunAsync(CancellationToken cancellationToken, Workspace workspace);
    }
}