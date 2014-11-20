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
    [Export(typeof(IFormattingRule))]
    [ExportMetadata("Order", 10)]
    internal sealed class HasUnderScoreInPrivateFieldNames : IFormattingRule
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
                var model = await solution.GetDocument(documentId).GetSemanticModelAsync(cancellationToken);
                var root = await model.SyntaxTree.GetRootAsync(cancellationToken) as CSharpSyntaxNode;
                var symbol = model.GetDeclaredSymbol(root.GetAnnotatedNodes(AnnotationMarker).ElementAt(i), cancellationToken);
                var newName = "_" + symbol.Name;
                solution = await Renamer.RenameSymbolAsync(solution, symbol, newName, solution.Workspace.Options, cancellationToken);
            }

            return solution;
        }
    }
}
