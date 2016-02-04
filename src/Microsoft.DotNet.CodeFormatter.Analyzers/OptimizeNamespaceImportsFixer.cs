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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class OptimizeNamespaceImportsFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                var usingDirectiveNodes = new List<SyntaxNode>();

                // We recapitulate the primary diagnostic location in the 
                // Diagnostic.AdditionalLocations property on raising
                // the diagnostic, so this member has complete location details.
                foreach (Location location in diagnostic.AdditionalLocations)
                {
                    SyntaxNode usingDirectiveNode = root.FindNode(location.SourceSpan);
                    Debug.Assert(usingDirectiveNode != null);
                    usingDirectiveNodes.Add(usingDirectiveNode);
                }

                var usingAction = (OptimizeNamespaceImportsAnalyzer.Action)Enum.Parse(typeof(OptimizeNamespaceImportsAnalyzer.Action), diagnostic.Properties["Action"]);
                switch (usingAction)
                {
                    case OptimizeNamespaceImportsAnalyzer.Action.Remove:
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                Resources.OptimizeNamespaceImportsFixer_Title,
                                c => RemoveUsingStatement(context.Document, root, usingDirectiveNodes)),
                            diagnostic);
                        break;
                    case OptimizeNamespaceImportsAnalyzer.Action.PlaceOutsideNamespace:
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                Resources.OptimizeNamespaceImportsFixer_Title,
                                c => PlaceOutsideNamespace(context.Document, root, usingDirectiveNodes)),
                            diagnostic);
                        break;
                }
            }
        }

        private Task<Document> RemoveUsingStatement(Document document, SyntaxNode root, IEnumerable<SyntaxNode> usingDirectiveNodes)
        {     
            return Task.FromResult(
                document.WithSyntaxRoot(root.RemoveNodes(usingDirectiveNodes, SyntaxRemoveOptions.KeepLeadingTrivia)));
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
                var newDirective = editor.Generator.WithName(usingDirective, symbol.ToDisplayString());
                editor.RemoveNode(usingDirective);
                newUsings.Add(newDirective);
            }

            var newRoot = editor.GetChangedRoot();
            newRoot = editor.Generator.AddNamespaceImports(newRoot, newUsings);
            editor.ReplaceNode(root, newRoot);
            return editor.GetChangedDocument();
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(OptimizeNamespaceImportsAnalyzer.DiagnosticId);
    }
}
