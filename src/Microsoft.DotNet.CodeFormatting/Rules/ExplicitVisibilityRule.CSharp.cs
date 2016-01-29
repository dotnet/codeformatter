// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    internal partial class ExplicitVisibilityRule
    {
        private sealed class CSharpVisibilityRewriter : CSharpSyntaxRewriter
        {
            private readonly Document _document;
            private readonly CancellationToken _cancellationToken;
            private SemanticModel _semanticModel;

            internal CSharpVisibilityRewriter(Document document, CancellationToken cancellationToken)
            {
                _document = document;
                _cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax originalNode)
            {
                var node = (ClassDeclarationSyntax)base.VisitClassDeclaration(originalNode);
                return EnsureTypeVisibility(node, (x, t) => x.WithKeyword(t), (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(originalNode));
            }

            public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
            {
                return EnsureTypeVisibility(node, (x, t) => x.WithKeyword(t), (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(node));
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax originalNode)
            {
                var node = (StructDeclarationSyntax)base.VisitStructDeclaration(originalNode);
                return EnsureTypeVisibility(node, (x, t) => x.WithKeyword(t), (x, l) => x.WithModifiers(l), () => GetTypeDefaultVisibility(originalNode));
            }

            public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax originalNode)
            {
                return EnsureVisibilityBeforeToken(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.DelegateKeyword,
                    (n, k) => n.WithDelegateKeyword(k),
                    (n, l) => n.WithModifiers(l),
                    () => GetDelegateTypeDefaultVisibility(originalNode));
            }

            public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax originalNode)
            {
                return EnsureVisibilityBeforeToken(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.EnumKeyword,
                    (n, k) => n.WithEnumKeyword(k),
                    (n, l) => n.WithModifiers(l),
                    () => GetTypeDefaultVisibility(originalNode));
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax originalNode)
            {
                if (originalNode.Modifiers.Any(x => x.Kind() == SyntaxKind.StaticKeyword))
                {
                    return originalNode;
                }

                return EnsureVisibilityBeforeToken(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.Identifier,
                    (n, t) => n.WithIdentifier(t),
                    (n, l) => n.WithModifiers(l),
                    () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax originalNode)
            {
                if (originalNode.ExplicitInterfaceSpecifier != null || originalNode.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
                {
                    return originalNode;
                }

                return EnsureVisibilityBeforeType(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.ReturnType,
                    (n, t) => n.WithReturnType(t),
                    (n, l) => n.WithModifiers(l),
                    () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax originalNode)
            {
                if (originalNode.ExplicitInterfaceSpecifier != null)
                {
                    return originalNode;
                }

                return EnsureVisibilityBeforeType(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.Type,
                    (n, t) => n.WithType(t),
                    (n, l) => n.WithModifiers(l),
                    () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax originalNode)
            {
                if (originalNode.ExplicitInterfaceSpecifier != null)
                {
                    return originalNode;
                }

                return EnsureVisibilityBeforeToken(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.EventKeyword,
                    (n, t) => n.WithEventKeyword(t),
                    (n, l) => n.WithModifiers(l),
                    () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax originalNode)
            {
                return EnsureVisibilityBeforeToken(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.EventKeyword,
                    (n, t) => n.WithEventKeyword(t),
                    (n, l) => n.WithModifiers(l),
                    () => SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax originalNode)
            {
                return EnsureVisibilityBeforeType(
                    originalNode,
                    originalNode.Modifiers,
                    (n) => n.Declaration.Type,
                    (n, t) => n.WithDeclaration(n.Declaration.WithType(t)),
                    (n, l) => n.WithModifiers(l),
                    () => SyntaxKind.PrivateKeyword);
            }

            private SyntaxKind GetTypeDefaultVisibility(BaseTypeDeclarationSyntax originalDeclarationSyntax)
            {
                // In the case of partial types we need to use the existing visibility if it exists
                if (originalDeclarationSyntax.Modifiers.Any(x => x.Kind() == SyntaxKind.PartialKeyword))
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
                    if (SyntaxFacts.IsAccessibilityModifier(token.Kind()))
                    {
                        return token.Kind();
                    }
                }

                return null;
            }

            private static bool IsNestedDeclaration(SyntaxNode node)
            {
                var current = node.Parent;
                while (current != null)
                {
                    if (SyntaxFacts.IsTypeDeclaration(current.Kind()))
                    {
                        return true;
                    }

                    current = current.Parent;
                }

                return false;
            }

            /// <summary>
            /// Ensure a visibility modifier is specified on the given type node.
            /// </summary>
            private static T EnsureTypeVisibility<T>(T originalNode, Func<T, SyntaxToken, T> withKeyword, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : TypeDeclarationSyntax
            {
                return EnsureVisibilityBeforeToken(
                    originalNode,
                    originalNode.Modifiers,
                    n => n.Keyword,
                    withKeyword,
                    withModifiers,
                    getDefaultVisibility);
            }

            /// <summary>
            /// Ensure a visibility modifier is specified.  If this is the first modifier it will be
            /// inserted directly before the returned token.
            /// </summary>
            private static T EnsureVisibilityBeforeToken<T>(T originalNode, SyntaxTokenList originalModifierList, Func<T, SyntaxToken> getToken, Func<T, SyntaxToken, T> withToken, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : CSharpSyntaxNode
            {
                Func<T, SyntaxKind, T> withFirstModifier = (node, visibilityKind) =>
                    {
                        var token = getToken(node);
                        var modifierList = CreateFirstModifierList(token.LeadingTrivia, visibilityKind);
                        node = withToken(node, token.WithLeadingTrivia());
                        node = withModifiers(node, modifierList);
                        return node;
                    };

                return EnsureVisibilityCore(
                    originalNode,
                    originalModifierList,
                    withFirstModifier,
                    withModifiers,
                    getDefaultVisibility);
            }

            /// <summary>
            /// Ensure a visibility modifier is specified.  If this is the first modifier it will be
            /// inserted directly before the specified type node.  
            /// </summary>
            private static T EnsureVisibilityBeforeType<T>(T originalNode, SyntaxTokenList originalModifierList, Func<T, TypeSyntax> getTypeSyntax, Func<T, TypeSyntax, T> withTypeSyntax, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : CSharpSyntaxNode
            {
                Func<T, SyntaxKind, T> withFirstModifier = (node, visibilityKind) =>
                    {
                        var typeSyntax = getTypeSyntax(node);
                        var modifierList = CreateFirstModifierList(typeSyntax.GetLeadingTrivia(), visibilityKind);
                        node = withTypeSyntax(node, typeSyntax.WithLeadingTrivia());
                        node = withModifiers(node, modifierList);
                        return node;
                    };

                return EnsureVisibilityCore(
                    originalNode,
                    originalModifierList,
                    withFirstModifier,
                    withModifiers,
                    getDefaultVisibility);
            }

            private static T EnsureVisibilityCore<T>(
                T originalNode,
                SyntaxTokenList originalModifierList,
                Func<T, SyntaxKind, T> withFirstModifier,
                Func<T, SyntaxTokenList, T> withModifiers,
                Func<SyntaxKind> getDefaultVisibility) where T : CSharpSyntaxNode
            {
                if (originalModifierList.Any(x => SyntaxFacts.IsAccessibilityModifier(x.Kind())))
                {
                    return originalNode;
                }

                SyntaxKind visibilityKind = getDefaultVisibility();
                Debug.Assert(SyntaxFacts.IsAccessibilityModifier(visibilityKind));

                if (originalModifierList.Count == 0)
                {
                    return withFirstModifier(originalNode, visibilityKind);
                }

                var leadingTrivia = originalModifierList[0].LeadingTrivia;
                var visibilityToken = SyntaxFactory.Token(
                    leadingTrivia,
                    visibilityKind,
                    SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

                var list = new List<SyntaxToken>();
                list.Add(visibilityToken);
                list.Add(originalModifierList[0].WithLeadingTrivia());
                for (int i = 1; i < originalModifierList.Count; i++)
                {
                    list.Add(originalModifierList[i]);
                }

                return withModifiers(originalNode, SyntaxFactory.TokenList(list));
            }

            private static SyntaxTokenList CreateFirstModifierList(SyntaxTriviaList leadingTrivia, SyntaxKind visibilityKind)
            {
                var visibilityToken = SyntaxFactory.Token(
                    leadingTrivia,
                    visibilityKind,
                    SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

                return SyntaxFactory.TokenList(visibilityToken);
            }
        }
    }
}
