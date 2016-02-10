// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    class PlaceImportsOutsideNamespaceFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PlaceImportsOutsideNamespaceAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var usingDirectiveNodes = new List<SyntaxNode>();

            // We recapitulate the primary diagnostic location in the 
            // Diagnostic.AdditionalLocations property on raising
            // the diagnostic, so this member has complete location details.
            foreach (Location location in diagnostic.AdditionalLocations)
            {
                SyntaxNode usingDirectiveNode = root.FindNode(location.SourceSpan);
                Debug.Assert(usingDirectiveNode != null && usingDirectiveNode.IsKind(SyntaxKind.UsingDirective));
                usingDirectiveNodes.Add(usingDirectiveNode);
            }

            context.RegisterCodeFix(
                        CodeAction.Create(
                            Resources.OptimizeNamespaceImportsFixer_Title,
                            c => PlaceOutsideNamespace(context.Document, root, usingDirectiveNodes)),
                        diagnostic);
        }

        private async Task<Document> PlaceOutsideNamespace(Document document, SyntaxNode root, IEnumerable<SyntaxNode> usingDirectiveNodes)
        {
            var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document).ConfigureAwait(false);

            var newUsings = new List<SyntaxNode>();
            foreach (var node in usingDirectiveNodes)
            {
                var usingDirective = node as UsingDirectiveSyntax;
                var symbol = semanticModel.GetSymbolInfo(usingDirective.Name).Symbol;
                if (symbol != null)
                {
                    var newDirective = editor.Generator.WithName(usingDirective, symbol.ToDisplayString())
                                       .WithAdditionalAnnotations(Formatter.Annotation)
                                       .WithTriviaFrom(usingDirective);
                    editor.RemoveNode(usingDirective);
                    newUsings.Add(newDirective);
                }
            }


            var newRoot = editor.GetChangedRoot();

            if (newUsings.Any())
            {
                // Add a blank line to the last using statement so that it's demarcated from the rest of the code.
                newUsings[newUsings.Count - 1] = AddBlankLine(newUsings.Last());

                // If the usings are added to the top of the file, we need to attach the leading trivia to it. 
                // To do that, store away the leading trivia from the root and re-attach it once the usings have been added.
                var leadingTrivia = newRoot.GetLeadingTrivia();
                newRoot = newRoot.WithoutLeadingTrivia();
                newRoot = editor.Generator.AddNamespaceImports(newRoot, newUsings);
                newRoot = newRoot.WithLeadingTrivia(leadingTrivia);
            }

            editor.ReplaceNode(root, newRoot);
            return editor.GetChangedDocument();
        }

        private SyntaxNode AddBlankLine(SyntaxNode syntaxNode)
        {
            var trailingTrivia = syntaxNode.GetTrailingTrivia();
            return syntaxNode.WithTrailingTrivia(trailingTrivia.Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
        }
    }
}
