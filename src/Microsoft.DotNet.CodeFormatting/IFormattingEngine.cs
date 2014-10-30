// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IFormattingEngine
    {
        Task<bool> RunAsync(Workspace workspace, CancellationToken cancellationToken);
    }
}