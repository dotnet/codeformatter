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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class UnwrittenWritableFieldThisFixer : CodeFixProvider
    {
        private static SyntaxToken s_readOnlyToken =  SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword);

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

        private Task<Document> AddReadonlyModifier(Document document, SyntaxNode root, FieldDeclarationSyntax fieldDeclaration)
        {
            FieldDeclarationSyntax newFieldDeclaration = fieldDeclaration
                .WithModifiers(fieldDeclaration.Modifiers.Add(s_readOnlyToken))
                .WithAdditionalAnnotations(Formatter.Annotation);
            SyntaxNode newRoot = root.ReplaceNode(fieldDeclaration, newFieldDeclaration);
            Document newDocument = document.WithSyntaxRoot(newRoot);
            return Task.FromResult(newDocument);
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(UnwrittenWritableFieldAnalyzer.DiagnosticId);
    }
}
