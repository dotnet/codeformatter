// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

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
        string FormattingOptionsFilePath { get; set; }
        bool ApplyFixes { get; set; }
        string LogOutputPath { get; set; }

        void ToggleRuleEnabled(IRuleMetadata ruleMetaData, bool enabled);
        Task FormatSolutionAsync(Solution solution, bool useAnalyzers, CancellationToken cancellationToken);
        Task FormatProjectAsync(Project project, bool useAnalyzers, CancellationToken cancellationToken);
        void AddAnalyzers(ImmutableArray<DiagnosticAnalyzer> immutableArray);
    }
}
