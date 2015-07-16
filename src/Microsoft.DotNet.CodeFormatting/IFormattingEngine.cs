// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IFormattingEngine
    {
        ImmutableArray<string> CopyrightHeader { get; set; }
        ImmutableArray<string[]> PreprocessorConfigurations { get; set; }
        ImmutableArray<string> FileNames { get; set; }
        ImmutableArray<IRuleMetadata> AllRules { get; }
        ImmutableArray<DiagnosticDescriptor> AllSupportedDiagnostics { get; }
        bool AllowTables { get; set; }
        bool Verbose { get; set; }
        void ToggleRuleEnabled(IRuleMetadata ruleMetaData, bool enabled);
        void ToggleDiagnosticEnabled(string diagnosticId, bool enabled);
        Task FormatSolutionAsync(Solution solution, bool useAnalyzers, CancellationToken cancellationToken);
        Task FormatProjectAsync(Project project, bool useAnalyzers, CancellationToken cancellationToken);
    }
}
