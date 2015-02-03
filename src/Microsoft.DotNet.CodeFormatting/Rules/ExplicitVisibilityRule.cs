// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRuleOrder(LocalSemanticRuleOrder.ExplicitVisibilityRule)]
    internal sealed class ExplicitVisibilityRule : ILocalSemanticFormattingRule
    {
        private sealed class VisibilityRewriter : CSharpSyntaxRewriter
        {
            private readonly Document _document;
            private readonly CancellationToken _cancellationToken;
            private SemanticModel _semanticModel;

            internal VisibilityRewriter(Document document, CancellationToken cancellationToken)
            {
                _document = document;
                _cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax originalNode)
            {
                var node = (ClassDeclarationSyntax)base.VisitClassDeclaration(originalNode);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(originalNode));
            }

            public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(node));
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax originalNode)
            {
                var node = (StructDeclarationSyntax)base.VisitStructDeclaration(originalNode);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(originalNode));
            }

            public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => GetDelegateTypeDefaultVisibility(node));
            }

            public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(node));
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                if (node.Modifiers.Any(x => x.CSharpKind() == SyntaxKind.StaticKeyword))
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (node.ExplicitInterfaceSpecifier != null || node.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (node.ExplicitInterfaceSpecifier != null)
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node)
            {
                if (node.ExplicitInterfaceSpecifier != null)
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PrivateKeyword);
            }

            private SyntaxKind GetTypeDefaultVisibility(BaseTypeDeclarationSyntax originalDeclarationSyntax)
            {
                // In the case of partial types we need to use the existing visibility if it exists
                if (originalDeclarationSyntax.Modifiers.Any(x => x.CSharpContextualKind() == SyntaxKind.PartialKeyword))
                {
                    SyntaxKind? kind = GetExistingPartialVisibility(originalDeclarationSyntax);
                    if (kind.HasValue)
                    {
                        return kind.Value;
                    }
                }

                return GetDelegateTypeDefaultVisibility(originalDeclarationSyntax);
            }

            private SyntaxKind GetDelegateTypeDefaultVisibility(SyntaxNode node)
            {
                return IsNestedDeclaration(node) ? SyntaxKind.PrivateKeyword : SyntaxKind.InternalKeyword;
            }

            private SyntaxKind? GetExistingPartialVisibility(BaseTypeDeclarationSyntax originalDeclarationSyntax)
            {
                // Getting the SemanticModel is a relatively expensive operation.  Can take a few seconds in 
                // projects of significant size.  It is delay created to avoid this in files which already
                // conform to the standards.
                if (_semanticModel == null)
                {
                    _semanticModel = _document.GetSemanticModelAsync(_cancellationToken).Result;
                }

                var symbol = _semanticModel.GetDeclaredSymbol(originalDeclarationSyntax, _cancellationToken);
                if (symbol == null)
                {
                    return null;
                }

                switch (symbol.DeclaredAccessibility)
                {
                    case Accessibility.Friend:
                        return SyntaxKind.InternalKeyword;
                    case Accessibility.Public:
                        return SyntaxKind.PublicKeyword;
                    case Accessibility.Private:
                        return SyntaxKind.PrivateKeyword;
                    case Accessibility.Protected:
                        return SyntaxKind.ProtectedKeyword;
                    default: return null;
                }
            }

            private static SyntaxKind? GetVisibilityModifier(SyntaxTokenList list)
            {
                foreach (var token in list)
                {
                    if (SyntaxFacts.IsAccessibilityModifier(token.CSharpKind()))
                    {
                        return token.CSharpKind();
                    }
                }

                return null;
            }

            private static bool IsNestedDeclaration(SyntaxNode node)
            {
                var current = node.Parent;
                while (current != null)
                {
                    if (SyntaxFacts.IsTypeDeclaration(current.CSharpKind()))
                    {
                        return true;
                    }

                    current = current.Parent;
                }

                return false;
            }

            /// <summary>
            /// Return a node declaration that has a visibility modifier.  If one isn't present it will be added as the 
            /// first modifier.  Any trivia before the node will be added as leading trivia to the added <see cref="SyntaxToken"/>.
            /// </summary>
            private static MemberDeclarationSyntax EnsureVisibility<T>(T node, SyntaxTokenList originalModifiers, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : MemberDeclarationSyntax
            {
                if (originalModifiers.Any(x => SyntaxFacts.IsAccessibilityModifier(x.CSharpKind())))
                {
                    return node;
                }

                SyntaxKind visibilityKind = getDefaultVisibility();
                Debug.Assert(SyntaxFacts.IsAccessibilityModifier(visibilityKind));

                SyntaxTokenList modifierList;
                if (originalModifiers.Count == 0)
                {
                    var leadingTrivia = node.GetLeadingTrivia();
                    node = node.WithLeadingTrivia();

                    var visibilityToken = SyntaxFactory.Token(
                        leadingTrivia,
                        visibilityKind,
                        SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

                    modifierList = SyntaxFactory.TokenList(visibilityToken);
                }
                else
                {
                    var leadingTrivia = originalModifiers[0].LeadingTrivia;
                    var visibilityToken = SyntaxFactory.Token(
                        leadingTrivia,
                        visibilityKind,
                        SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

                    var list = new List<SyntaxToken>();
                    list.Add(visibilityToken);
                    list.Add(originalModifiers[0].WithLeadingTrivia());
                    for (int i = 1; i < originalModifiers.Count; i++)
                    {
                        list.Add(originalModifiers[i]);
                    }

                    modifierList = SyntaxFactory.TokenList(list);
                }

                return withModifiers(node, modifierList);
            }
        }

        public Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var rewriter = new VisibilityRewriter(document, cancellationToken);
            var newNode = rewriter.Visit(syntaxNode);
            return Task.FromResult(newNode);
        }
    }
}
