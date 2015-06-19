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
            private ImmutableDictionary<string, CodeFixProvider> _fixerMap;

            public UberCodeFixer(ImmutableDictionary<string, CodeFixProvider> fixerMap)
            {
                _fixerMap = fixerMap;
            }

            public override async Task RegisterCodeFixesAsync(CodeFixContext context)
            {
                foreach (var diagnostic in context.Diagnostics)
                {
                    var fixer = _fixerMap[diagnostic.Id];
                    await fixer.RegisterCodeFixesAsync(new CodeFixContext(context.Document, diagnostic, (a, d) => context.RegisterCodeFix(a, d), context.CancellationToken));
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
