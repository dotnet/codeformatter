// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class ExplicitThisFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
            var memberAccessNode = root
                .FindToken(diagnosticSpan.Start)
                .Parent
                .FirstAncestorOrSelf<MemberAccessExpressionSyntax>();

            Debug.Assert(memberAccessNode != null);

            context.RegisterCodeFix(
                CodeAction.Create(
                    Resources.ExplicitThisFixer_Title,
                    c => RemoveThisQualifier(context.Document, root, memberAccessNode)),
                context.Diagnostics.First());
        }

        private Task<Document> RemoveThisQualifier(Document document, SyntaxNode root, SyntaxNode memberAccessNode)
        {
            return Task.FromResult(
                document.WithSyntaxRoot(root.ReplaceNode(memberAccessNode, memberAccessNode.WithAdditionalAnnotations(Simplifier.Annotation))));
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(ExplicitThisAnalyzer.DiagnosticId);
    }
}
