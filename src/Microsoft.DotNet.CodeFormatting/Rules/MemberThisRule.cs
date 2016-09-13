// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRule(MemberThisRule.Name, MemberThisRule.Description, LocalSemanticRuleOrder.MemberThisRule, DefaultRule = false)]
    internal sealed class MemberThisRule : CSharpOnlyFormattingRule, ILocalSemanticFormattingRule
    {
        internal const string Name = "MemberThis";
        internal const string Description = "Add this/Me prefixes on all member references";

        private sealed class MemberThisRewriter : CSharpSyntaxRewriter
        {
            private SemanticModel semanticModel;
            private readonly CancellationToken cancellationToken;

            internal bool AddedAnnotations
            {
                get; private set;
            }

            internal MemberThisRewriter(SemanticModel semanticModel, CancellationToken cancellationToken)
            {
                this.semanticModel = semanticModel;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitGenericName(GenericNameSyntax name)
            {
                if (this.ShouldModify(name))
                {
                    this.AddedAnnotations = true;

                    // Create a copy of this name
                    GenericNameSyntax newName = SyntaxFactory.GenericName(name.Identifier, name.TypeArgumentList);

                    // Add the "this" to the expression
                    return SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        newName.WithoutTrivia())
                        .WithLeadingTrivia(name.GetLeadingTrivia())
                        .WithTrailingTrivia(name.GetTrailingTrivia());
                }

                return base.VisitGenericName(name);
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax name)
            {
                if (this.ShouldModify(name))
                {
                    this.AddedAnnotations = true;

                    // Create a copy of this name
                    IdentifierNameSyntax newName = SyntaxFactory.IdentifierName(name.Identifier);

                    // Add the "this" to the expression
                    return SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        newName.WithoutTrivia())
                        .WithLeadingTrivia(name.GetLeadingTrivia())
                        .WithTrailingTrivia(name.GetTrailingTrivia());
                }

                return base.VisitIdentifierName(name);
            }

            private bool ShouldModify(SimpleNameSyntax name)
            {
                // Ignore cases that don't make sense
                if (name.Parent is MemberAccessExpressionSyntax)
                {
                    MemberAccessExpressionSyntax parent = (MemberAccessExpressionSyntax)name.Parent;
                    if (parent.Name == name)
                    {
                        return false;
                    }
                }
                else if (name.Parent is QualifiedNameSyntax)
                {
                    QualifiedNameSyntax parent = (QualifiedNameSyntax)name.Parent;
                    if (parent.Left == name)
                    {
                        return false;
                    }
                }
                else if (name.Parent is MemberBindingExpressionSyntax || name.Parent is AliasQualifiedNameSyntax)
                {
                    return false;
                }

                return this.IsLocalMember(name);
            }

            // Check if the name referes to a non-static member on the same type.
            private bool IsLocalMember(SimpleNameSyntax name)
            {
                ISymbol symbol = semanticModel.GetSymbolInfo(name, this.cancellationToken).Symbol;

                // Only add the "this" to non-static member references
                if (symbol == null || symbol.IsStatic || !IsMember(symbol))
                {
                    return false;
                }

                // Make sure the reference is in the same type
                return IsDerivedType(this.GetParentType(name), symbol.ContainingType);
            }

            private static bool IsDerivedType(INamedTypeSymbol type, INamedTypeSymbol baseType)
            {
                if (type == null)
                    return false;
                if (object.Equals(type, baseType))
                    return true;
                return IsDerivedType(type.BaseType, baseType);
            }

            private static bool IsMember(ISymbol symbol)
            {
                switch (symbol.Kind)
                {
                    case SymbolKind.Event:
                    case SymbolKind.Field:
                    case SymbolKind.Method:
                    case SymbolKind.Property:
                        return true;
                    default:
                        return false;
                }
            }

            // Find the type this node resides in
            private INamedTypeSymbol GetParentType(SyntaxNode node)
            {
                while (node != null)
                {
                    if (node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration))
                    {
                        return (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(node);
                    }

                    node = node.Parent;
                }
                return null;
            }
        }

        /// <summary>
        /// Entry point to the rule
        /// </summary>
        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            // Visit all the nodes in the model
            MemberThisRewriter rewriter = new MemberThisRewriter(semanticModel, cancellationToken);
            SyntaxNode newNode = rewriter.Visit(syntaxNode);

            // If changes are not mode return the original model
            if (!rewriter.AddedAnnotations)
            {
                return syntaxNode;
            }

            return newNode;
        }
    }
}
