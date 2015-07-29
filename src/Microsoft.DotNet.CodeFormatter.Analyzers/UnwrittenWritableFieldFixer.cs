// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
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
                    c => AddReadonlyModifier(context.Document, root, fieldDeclarationNode, context.CancellationToken)),
                diagnostic);
        }

        private async Task<Document> AddReadonlyModifier(Document document, SyntaxNode root, FieldDeclarationSyntax fieldDeclaration, CancellationToken cancellationToken)
        {
            var docEditor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var modifiers = docEditor.Generator.GetModifiers(fieldDeclaration);
            docEditor.SetModifiers(fieldDeclaration, modifiers + DeclarationModifiers.ReadOnly);

            return docEditor.GetChangedDocument();
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(UnwrittenWritableFieldAnalyzer.DiagnosticId);
    }
}
