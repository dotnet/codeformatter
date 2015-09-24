// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.DotNet.CodeFormatting
{
    internal sealed partial class FormattingEngineImplementation
    {
        private class UberCodeFixer : CodeFixProvider
        {
            private ImmutableDictionary<string, CodeFixProvider> _diagnosticIdToFixerMap;

            public UberCodeFixer(ImmutableDictionary<string, CodeFixProvider> diagnosticIdToFixerMap)
            {
                _diagnosticIdToFixerMap = diagnosticIdToFixerMap;
            }

            public override async Task RegisterCodeFixesAsync(CodeFixContext context)
            {
                foreach (var diagnostic in context.Diagnostics)
                {
                    var fixer = _diagnosticIdToFixerMap[diagnostic.Id];
                    await fixer.RegisterCodeFixesAsync(new CodeFixContext(context.Document, diagnostic, (a, d) => context.RegisterCodeFix(a, d), context.CancellationToken)).ConfigureAwait(false);
                }
            }

            public override FixAllProvider GetFixAllProvider()
            {
                return null;
            }

            public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray<string>.Empty;
        }
    }
}
