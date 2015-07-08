// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRule(Name, Description, LocalSemanticRuleOrder.HasNoUnusedUsingsRule)]
    internal sealed class HasNoUnusedUsingsRule : CSharpOnlyFormattingRule, ILocalSemanticFormattingRule
    {
        internal const string Name = "HasNoUnusedUsings";
        internal const string Description = "Removed unused using declarations.";
        private const string UnusedUsingDiagnosticId = "CS8019";

        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken)
        {
            var root = syntaxRoot as CompilationUnitSyntax;
            if (root == null)
                return syntaxRoot;

            if (root.DescendantTrivia().Any(x => x.Kind() == SyntaxKind.IfDirectiveTrivia))
                return syntaxRoot;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var diagnostics = semanticModel.GetDiagnostics(null, cancellationToken);

            var unusedUsingDiagnostics = diagnostics
                .Where(x => x.Id == UnusedUsingDiagnosticId)
                .Select(x => x.Location.SourceSpan.Start)
                .ToList()
                .AsReadOnly();

            if (unusedUsingDiagnostics.Count == 0)
                return syntaxRoot;

            var unusedUsings = root.Usings
                .Where(x => unusedUsingDiagnostics.Any(unusedUsing => x.Span.IntersectsWith(unusedUsing)))
                .ToList()
                .AsReadOnly();

            return syntaxRoot.RemoveNodes(unusedUsings, SyntaxRemoveOptions.KeepLeadingTrivia);
        }
    }
}
