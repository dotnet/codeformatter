// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IFormattingEngine
    {
        ImmutableArray<string> CopyrightHeader { get; set; }
        ImmutableArray<string[]> PreprocessorConfigurations { get; set; }
        ImmutableArray<string> FileNames { get; set; }
        bool AllowTables { get; set; }
        bool ConvertUnicodeCharacters { get; set; }
        bool Verbose { get; set; }
        Task FormatSolutionAsync(Solution solution, CancellationToken cancellationToken);
        Task FormatProjectAsync(Project porject, CancellationToken cancellationToken);
    }
}
