// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(10)]
    // TODO Bug 1086632: Deactivated due to active bug in Roslyn.
    // There is a hack to run this rule, but it's slow. 
    // If needed, enable the rule and enable the hack at the code below in RenameFields.
    internal sealed class HasUnderScoreInPrivateFieldNamesFormattingRule : IFormattingRule
    {
        private static string[] AccessorModifiers = { "public", "internal", "protected", "const" };

        private static readonly SyntaxAnnotation AnnotationMarker = new SyntaxAnnotation();

        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;

            var typeNodes = syntaxRoot.DescendantNodes().Where(type => type.CSharpKind() == SyntaxKind.ClassDeclaration || type.CSharpKind() == SyntaxKind.StructDeclaration);
            IEnumerable<SyntaxNode> privateFields = GetPrivateFields(typeNodes);
            if (privateFields.Any())
            {
                var solutionWithAnnotation = GetSolutionWithRenameAnnotation(document, syntaxRoot, privateFields, cancellationToken);
                var solution = await RenameFields(solutionWithAnnotation, document.Id, privateFields, cancellationToken);
                return solution.GetDocument(document.Id);
            }

            return document;
        }

        private IEnumerable<SyntaxNode> GetPrivateFields(IEnumerable<SyntaxNode> typeNodes)
        {
            IEnumerable<SyntaxNode> privateFields = Enumerable.Empty<SyntaxNode>();
            foreach (var type in typeNodes)
            {
                privateFields = privateFields.Concat(type.ChildNodes().OfType<FieldDeclarationSyntax>()
                    .Where(f => !AccessorModifiers.Any(f.Modifiers.ToString().Contains)).SelectMany(f => f.Declaration.Variables))
                    .Where(v => !(v as VariableDeclaratorSyntax).Identifier.Text.StartsWith("_"));
            }

            return privateFields;
        }

        private Solution GetSolutionWithRenameAnnotation(Document document, SyntaxNode syntaxRoot, IEnumerable<SyntaxNode> privateFields, CancellationToken cancellationToken)
        {
            Func<SyntaxNode, SyntaxNode, SyntaxNode> addAnnotation = (variable, dummy) =>
            {
                return variable.WithAdditionalAnnotations(AnnotationMarker);
            };

            return document.WithSyntaxRoot(syntaxRoot.ReplaceNodes(privateFields, addAnnotation)).Project.Solution;
        }

        private async Task<Solution> RenameFields(Solution solution, DocumentId documentId, IEnumerable<SyntaxNode> privateFields, CancellationToken cancellationToken)
        {
            int count = privateFields.Count();
            for (int i = 0; i < count; i++)
            {
                // This is a hack to till the roslyn bug is fixed. Very slow, enable this statement only if the rule is enabled.
                solution = await CleanSolutionAsync(solution, cancellationToken);
                var model = await solution.GetDocument(documentId).GetSemanticModelAsync(cancellationToken);
                var root = await model.SyntaxTree.GetRootAsync(cancellationToken) as CSharpSyntaxNode;
                var symbol = model.GetDeclaredSymbol(root.GetAnnotatedNodes(AnnotationMarker).ElementAt(i), cancellationToken);
                var newName = GetNewSymbolName(symbol);
                solution = await Renamer.RenameSymbolAsync(solution, symbol, newName, solution.Workspace.Options, cancellationToken).ConfigureAwait(false);
            }

            return solution;
        }

        private static string GetNewSymbolName(ISymbol symbol)
        {
            if (symbol.IsStatic)
            {
                // Check for ThreadStatic private fields.
                if (symbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals("ThreadStatic")))
                {
                    if (!symbol.Name.StartsWith("t_"))
                        return "t_" + symbol.Name;
                }
                else if (!symbol.Name.StartsWith("s_"))
                    return "s_" + symbol.Name;

                return symbol.Name;
            }

            return "_" + symbol.Name;
        }

        private async Task<Solution> CleanSolutionAsync(Solution solution, CancellationToken cancellationToken)
        {
            var documentIdsToProcess = new List<DocumentId>();
            foreach (var document in solution.Projects.SelectMany(p => p.Documents))
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                if (root.HasAnnotations("Rename") || true)
                {
                    documentIdsToProcess.Add(document.Id);
                }
            }

            foreach (var documentId in documentIdsToProcess)
            {
                var root = await solution.GetDocument(documentId).GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

                while (true)
                {
                    var renameNodes = root.DescendantNodes(descendIntoTrivia: true).Where(s => s.GetAnnotations("Rename").Any());
                    if (!renameNodes.Any())
                    {
                        break;
                    }

                    root = root.ReplaceNode(renameNodes.First(), renameNodes.First().WithoutAnnotations("Rename"));
                }

                while (true)
                {
                    var renameTokens = root.DescendantTokens(descendIntoTrivia: true).Where(s => s.GetAnnotations("Rename").Any());
                    if (!renameTokens.Any())
                    {
                        break;
                    }

                    root = root.ReplaceToken(renameTokens.First(), renameTokens.First().WithoutAnnotations("Rename"));
                }

                solution = solution.WithDocumentSyntaxRoot(documentId, root);
            }

            return solution;
        }
    }
}
