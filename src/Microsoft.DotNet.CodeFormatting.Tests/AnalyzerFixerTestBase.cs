// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class AnalyzerFixerTestBase : CodeFormattingTestBase
    {
        private IFormattingEngine _engine;

        private IFormattingEngine Engine
        {
            get
            {
                if (_engine == null)
                {
                    _engine = FormattingEngine.Create();
                }

                return _engine;
            }
        }

        protected void DisableAllDiagnostics()
        {
            foreach (var supportedDiagnostic in Engine.AllSupportedDiagnostics)
            {
                Engine.ToggleDiagnosticEnabled(supportedDiagnostic.Id, false);
            }
        }

        protected void EnableDiagnostic(string diagnosticId)
        {
            Engine.ToggleDiagnosticEnabled(diagnosticId, true);
        }

        protected override async Task<Solution> Format(Solution solution, bool runFormatter)
        {
            Workspace workspace = solution.Workspace;
            await _engine.FormatSolutionAsync(solution, useAnalyzers: true, cancellationToken: default(CancellationToken)).ConfigureAwait(false);
            return workspace.CurrentSolution;
        }
    }
}
