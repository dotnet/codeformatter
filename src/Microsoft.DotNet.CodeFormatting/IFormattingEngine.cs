// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IFormattingEngine
    {
        bool Verbose { get; set; }
        Task FormatSolutionAsync(Solution solution, CancellationToken cancellationToken);
        Task FormatProjectAsync(Project porject, CancellationToken cancellationToken);
    }
}