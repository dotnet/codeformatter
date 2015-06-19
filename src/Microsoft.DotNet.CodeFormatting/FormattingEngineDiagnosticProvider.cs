// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.DotNet.CodeFormatting
{
    internal sealed partial class FormattingEngineImplementation
    {
        private class FormattingEngineDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly Project _project;
            private List<Diagnostic> _allDiagnostics;

            public FormattingEngineDiagnosticProvider(Project project, IEnumerable<Diagnostic> diagnostics)
            {
                _project = project;
                _allDiagnostics = new List<Diagnostic>(diagnostics);
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                if (project == _project)
                {
                    return Task.FromResult(_allDiagnostics.Where(d => true));
                }

                return Task.FromResult(Enumerable.Empty<Diagnostic>());
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                return Task.FromResult(_allDiagnostics.Where(d => d.Location.SourceTree.FilePath == document.FilePath));
            }

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return Task.FromResult(_allDiagnostics.Where(d => d.Location == Location.None));
            }
        }
    }
}
