// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class ExplicitVariableTypeFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(ExplicitVariableTypeAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel model = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            TypeSyntax oldTypeSyntaxNode = null;
            TypeSyntax newTypeSyntaxNode = null;
            switch (GetRuleType(diagnostic))
            {
                case RuleType.RuleVariableDeclaration:
                    var variableDeclarationNode = root.
                        FindNode(diagnosticSpan).
                        FirstAncestorOrSelf<VariableDeclarationSyntax>();
                    Debug.Assert(variableDeclarationNode != null);
                    oldTypeSyntaxNode = variableDeclarationNode.Type;
                    // Implicit typed variables cannot have multiple declartors
                    newTypeSyntaxNode = CreateTypeSyntaxNode(variableDeclarationNode.Variables.Single(), model, cancellationToken);
                    break;

                case RuleType.RuleForEachStatement:
                    var forEachStatementNode = root.
                        FindToken(diagnosticSpan.Start).
                        Parent.
                        FirstAncestorOrSelf<ForEachStatementSyntax>();
                    Debug.Assert(forEachStatementNode != null);
                    oldTypeSyntaxNode = forEachStatementNode.Type;
                    newTypeSyntaxNode = CreateTypeSyntaxNode(forEachStatementNode, model, cancellationToken);
                    break;

                case RuleType.None:
                    break;
            }

            if (oldTypeSyntaxNode != null && newTypeSyntaxNode != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Resources.ExplicitVariableTypeFixer_Title,
                        c => ReplaceVarWithExplicitType(context.Document, root, oldTypeSyntaxNode, newTypeSyntaxNode.WithTriviaFrom(oldTypeSyntaxNode))),
                    diagnostic);
            }
        }

        private Task<Document> ReplaceVarWithExplicitType(Document document, SyntaxNode root, SyntaxNode varNode, SyntaxNode explicitTypeNode)
        {
            return Task.FromResult(
                document.WithSyntaxRoot(root.ReplaceNode(varNode, explicitTypeNode.WithAdditionalAnnotations(Simplifier.Annotation))));
        }

        // return null if could not construct a TypeSyntax with explicit type.
        private static TypeSyntax CreateTypeSyntaxNode(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            if (node == null || model == null)
            {
                return null;
            }

            var symbol = (ILocalSymbol)model.GetDeclaredSymbol(node, cancellationToken); 
            ITypeSymbol typeSymbol = symbol.Type;
            // typeSymbol.IsAnonymousType is guaranteed to be false since we already filtered out anonymous type in analyzer
            Debug.Assert(!typeSymbol.IsAnonymousType);
            TypeSyntax newTypeSyntaxNode = null;
            switch (typeSymbol.TypeKind)
            {
                case TypeKind.Class:
                case TypeKind.Struct:
                case TypeKind.Enum:
                    newTypeSyntaxNode = SyntaxFactory.ParseTypeName(typeSymbol.Name, 0);
                    break;
                case TypeKind.Array:
                    // Need to handle multi-dimentional array
                    var arrayTypeSymbol = (IArrayTypeSymbol)typeSymbol;
                    ITypeSymbol elementTypeSymbol = arrayTypeSymbol.ElementType;
                    int dimension;
                    for (dimension = 1; 
                         elementTypeSymbol.TypeKind == TypeKind.Array; 
                         arrayTypeSymbol = (IArrayTypeSymbol)elementTypeSymbol, elementTypeSymbol = arrayTypeSymbol.ElementType, ++dimension)
                    { }
                    TypeSyntax elementTypeSyntaxNode = SyntaxFactory.ParseTypeName(elementTypeSymbol.Name, 0);
                    for (newTypeSyntaxNode = elementTypeSyntaxNode; dimension > 0; --dimension)
                    { 
                        newTypeSyntaxNode = SyntaxFactory.ArrayType(newTypeSyntaxNode).AddRankSpecifiers(SyntaxFactory.ArrayRankSpecifier());
                    }
                    break;
                default:
                    // TODO: Is there any other TypeKind needs to be implemented?
                    return null;
            }
            Debug.Assert(newTypeSyntaxNode.IsMissing == false);
            return newTypeSyntaxNode.WithTriviaFrom(node);
        }

        private static RuleType GetRuleType(Diagnostic diagnostic)
        {
            Debug.Assert(diagnostic != null);

            foreach (string customTag in diagnostic.Descriptor.CustomTags)
            {
                switch (customTag)
                {
                    case ExplicitVariableTypeAnalyzer.VariableDeclarationCustomTag:
                        return RuleType.RuleVariableDeclaration;
                    case ExplicitVariableTypeAnalyzer.ForEachStatementCustomTag:
                        return RuleType.RuleForEachStatement;
                    // Diagnostics corresponding to this fixer must has either one of these above two custom tags
                }
            }
            Debug.Fail("This program location is thought to be unreachable.");
            // This is just to make compiler happy
            return RuleType.None;
        }

        private enum RuleType
        {
            None = 0,
            RuleVariableDeclaration,
            RuleForEachStatement
        }
    }
}
