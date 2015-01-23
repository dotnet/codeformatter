// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [GlobalSemanticRuleOrder(GlobalSemanticRuleOrder.PrivateFieldNamingRule)]
    internal sealed class PrivateFieldNamingRule : IGlobalSemanticFormattingRule
    {
        /// <summary>
        /// This will add an annotation to any private field that needs to be renamed.
        /// </summary>
        internal sealed class PrivateFieldAnnotationsRewriter : CSharpSyntaxRewriter
        {
            internal readonly static SyntaxAnnotation Marker = new SyntaxAnnotation("PrivateFieldToRename");

            // Used to avoid the array allocation on calls to WithAdditionalAnnotations
            private readonly static SyntaxAnnotation[] s_markerArray;

            static PrivateFieldAnnotationsRewriter()
            {
                s_markerArray = new SyntaxAnnotation[] { Marker };
            }

            private int _count;

            internal static SyntaxNode AddAnnotations(SyntaxNode node, out int count)
            {
                var rewriter = new PrivateFieldAnnotationsRewriter();
                var newNode = rewriter.Visit(node);
                count = rewriter._count;
                return newNode;
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                bool isInstance;
                if (NeedsRewrite(node, out isInstance))
                {
                    var list = new List<VariableDeclaratorSyntax>(node.Declaration.Variables.Count);
                    foreach (var v in node.Declaration.Variables)
                    {
                        if (IsGoodName(v, isInstance))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(v.WithAdditionalAnnotations(Marker));
                            _count++;
                        }
                    }

                    var declaration = node.Declaration.WithVariables(SyntaxFactory.SeparatedList(list));
                    node = node.WithDeclaration(declaration);

                    return node;
                }

                return node;
            }

            private static bool NeedsRewrite(FieldDeclarationSyntax fieldSyntax, out bool isInstance)
            {
                if (!IsPrivateField(fieldSyntax, out isInstance))
                {
                    return false;
                }

                foreach (var v in fieldSyntax.Declaration.Variables)
                {
                    if (!IsGoodName(v, isInstance))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsGoodName(VariableDeclaratorSyntax node, bool isInstance)
            {
                var name = node.Identifier.ValueText;
                if (isInstance)
                {
                    return name.Length > 0 && name[0] == '_';
                }
                else
                {
                    return name.Length > 1 && (name[0] == 's' || name[0] == 't') && name[1] == '_';
                }
            }

            private static bool IsPrivateField(FieldDeclarationSyntax fieldSyntax, out bool isInstance)
            {
                isInstance = true;
                foreach (var modifier in fieldSyntax.Modifiers)
                {
                    switch (modifier.CSharpKind())
                    {
                        case SyntaxKind.PublicKeyword:
                        case SyntaxKind.ConstKeyword:
                        case SyntaxKind.InternalKeyword:
                        case SyntaxKind.ProtectedKeyword:
                            return false;
                        case SyntaxKind.StaticKeyword:
                            isInstance = false;
                            break;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// This rewriter exists to work around DevDiv 1086632 in Roslyn.  The Rename action is 
        /// leaving a set of annotations in the tree.  These annotations slow down further processing
        /// and eventually make the rename operation unusable.  As a temporary work around we manually
        /// remove these from the tree.
        /// </summary>
        private sealed class RemoveRenameAnnotationsRewriter : CSharpSyntaxRewriter
        {
            private const string RenameAnnotationName = "Rename";

            public override SyntaxNode Visit(SyntaxNode node)
            {
                node = base.Visit(node);
                if (node != null && node.ContainsAnnotations && node.GetAnnotations(RenameAnnotationName).Any())
                {
                    node = node.WithoutAnnotations(RenameAnnotationName);
                }

                return node;
            }

            public override SyntaxToken VisitToken(SyntaxToken token)
            {
                token = base.VisitToken(token);
                if (token.ContainsAnnotations && token.GetAnnotations(RenameAnnotationName).Any())
                {
                    token = token.WithoutAnnotations(RenameAnnotationName);
                }

                return token;
            }
        }

        public async Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken)
        {
            int count;
            var newSyntaxRoot = PrivateFieldAnnotationsRewriter.AddAnnotations(syntaxRoot, out count);

            if (count == 0)
            {
                return document.Project.Solution;
            }

            var documentId = document.Id;
            var solution = document.Project.Solution;
            solution = solution.WithDocumentSyntaxRoot(documentId, newSyntaxRoot);
            solution = await RenameFields(solution, documentId, count, cancellationToken);
            return solution;
        }

        private static async Task<Solution> RenameFields(Solution solution, DocumentId documentId, int count, CancellationToken cancellationToken)
        {
            Solution oldSolution = null;
            for (int i = 0; i < count; i++)
            {
                oldSolution = solution;

                var semanticModel = await solution.GetDocument(documentId).GetSemanticModelAsync(cancellationToken);
                var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken) as CSharpSyntaxNode;
                var declaration = root.GetAnnotatedNodes(PrivateFieldAnnotationsRewriter.Marker).ElementAt(i);
                var fieldSymbol = (IFieldSymbol)semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
                var newName = GetNewFieldName(fieldSymbol);

                // Can happen with pathologically bad field names like _
                if (newName == fieldSymbol.Name)
                {
                    continue;
                }

                solution = await Renamer.RenameSymbolAsync(solution, fieldSymbol, newName, solution.Workspace.Options, cancellationToken).ConfigureAwait(false);
                solution = await CleanSolutionAsync(solution, oldSolution, cancellationToken);
            }

            return solution;
        }

        private static string GetNewFieldName(IFieldSymbol fieldSymbol)
        {
            var name = fieldSymbol.Name.Trim('_');
            if (name.Length > 2 && char.IsLetter(name[0]) && name[1] == '_')
            {
                name = name.Substring(2);
            }

            if (name.Length == 0)
            {
                return fieldSymbol.Name;
            }

            if (fieldSymbol.IsStatic)
            {
                // Check for ThreadStatic private fields.
                if (fieldSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals("ThreadStaticAttribute", StringComparison.Ordinal)))
                {
                    return "t_" + name;
                }
                else
                {
                    return "s_" + name;
                }
            }

            return "_" + name;
        }

        private static async Task<Solution> CleanSolutionAsync(Solution newSolution, Solution oldSolution, CancellationToken cancellationToken)
        {
            var rewriter = new RemoveRenameAnnotationsRewriter();
            var solution = newSolution;

            foreach (var projectChange in newSolution.GetChanges(oldSolution).GetProjectChanges())
            {
                foreach (var documentId in projectChange.GetChangedDocuments())
                {
                    solution = await CleanSolutionDocument(rewriter, solution, documentId, cancellationToken);
                }
            }

            return solution;
        }

        private static async Task<Solution> CleanSolutionDocument(RemoveRenameAnnotationsRewriter rewriter, Solution solution, DocumentId documentId, CancellationToken cancellationToken)
        {
            var document = solution.GetDocument(documentId);
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
            {
                return solution;
            }

            var newNode = rewriter.Visit(syntaxNode);
            return solution.WithDocumentSyntaxRoot(documentId, newNode);
        }
    }
}
