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
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class UnwrittenWritableFieldThisFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
            var fieldDeclarationNode = root
                .FindToken(diagnosticSpan.Start)
                .Parent
                .FirstAncestorOrSelf<FieldDeclarationSyntax>();

            Debug.Assert(fieldDeclarationNode != null);

            context.RegisterCodeFix(
                CodeAction.Create(
                    Resources.UnwrittenWritableFieldFixer_Title,
                    c => AddReadonlyModifier(context.Document, root, fieldDeclarationNode)),
                diagnostic);
        }

        private Task<Document> AddReadonlyModifier(Document document, SyntaxNode root, SyntaxNode fieldDeclarationNode)
        {
            return Task.FromResult(document);
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(UnwrittenWritableFieldAnalyzer.DiagnosticId);
    }
}
