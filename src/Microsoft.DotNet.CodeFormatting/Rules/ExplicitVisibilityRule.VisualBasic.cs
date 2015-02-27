// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
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
        private sealed class VisualBasicVisibilityRewriter : VisualBasicSyntaxRewriter
        {
            private readonly Document _document;
            private readonly CancellationToken _cancellationToken;
            private SemanticModel _semanticModel;
            private bool _inModule;

            internal VisualBasicVisibilityRewriter(Document document, CancellationToken cancellationToken)
            {
                _document = document;
                _cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitClassBlock(ClassBlockSyntax originalNode)
            {
                var node = (ClassBlockSyntax)base.VisitClassBlock(originalNode);
                var begin = (ClassStatementSyntax)EnsureVisibility(
                    node.ClassStatement, 
                    node.ClassStatement.ClassKeyword,
                    node.ClassStatement.Modifiers, 
                    (x, k) => x.WithClassKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => GetTypeDefaultVisibility(originalNode));
                return node.WithClassStatement(begin);
            }

            public override SyntaxNode VisitStructureBlock(StructureBlockSyntax originalNode)
            {
                var node = (StructureBlockSyntax)base.VisitStructureBlock(originalNode);
                var begin = (StructureStatementSyntax)EnsureVisibility(
                    node.StructureStatement, 
                    node.StructureStatement.StructureKeyword,
                    node.StructureStatement.Modifiers, 
                    (x, k) => x.WithStructureKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => GetTypeDefaultVisibility(originalNode));
                return node.WithStructureStatement(begin);
            }

            public override SyntaxNode VisitInterfaceBlock(InterfaceBlockSyntax originalNode)
            {
                var node = (InterfaceBlockSyntax)base.VisitInterfaceBlock(originalNode);
                var begin = (InterfaceStatementSyntax)EnsureVisibility(
                    node.InterfaceStatement, 
                    node.InterfaceStatement.InterfaceKeyword,
                    node.InterfaceStatement.Modifiers, 
                    (x, k) => x.WithInterfaceKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => GetTypeDefaultVisibility(originalNode));
                return node.WithInterfaceStatement(begin);
            }

            public override SyntaxNode VisitModuleBlock(ModuleBlockSyntax node)
            {
                var savedInModule = _inModule;
                try
                {
                    _inModule = true;
                    node = (ModuleBlockSyntax)base.VisitModuleBlock(node);
                }
                finally
                {
                    _inModule = savedInModule;
                }

                var begin = (ModuleStatementSyntax)EnsureVisibility(
                    node.ModuleStatement, 
                    node.ModuleStatement.ModuleKeyword,
                    node.ModuleStatement.Modifiers, 
                    (x, k) => x.WithModuleKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => SyntaxKind.FriendKeyword);
                return node.WithModuleStatement(begin);
            }

            public override SyntaxNode VisitEnumBlock(EnumBlockSyntax node)
            {
                var enumStatement = (EnumStatementSyntax)EnsureVisibility(
                    node.EnumStatement, 
                    node.EnumStatement.EnumKeyword,
                    node.EnumStatement.Modifiers, 
                    (x, k) => x.WithEnumKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => GetDelegateTypeDefaultVisibility(node));
                return node.WithEnumStatement(enumStatement);
            }

            public override SyntaxNode VisitMethodStatement(MethodStatementSyntax node)
            {
                if (IsInterfaceMember(node))
                {
                    return node;
                }

                return EnsureVisibility(
                    node, 
                    node.SubOrFunctionKeyword,
                    node.Modifiers, 
                    (x, k) => x.WithSubOrFunctionKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => SyntaxKind.PublicKeyword);
            }

            public override SyntaxNode VisitSubNewStatement(SubNewStatementSyntax node)
            {
                if (node.Modifiers.Any(x => x.IsKind(SyntaxKind.SharedKeyword)) || _inModule)
                {
                    return node;
                }

                return EnsureVisibility(
                    node, 
                    node.SubKeyword,
                    node.Modifiers, 
                    (x, k) => x.WithSubKeyword(k),
                    (x, l) => x.WithModifiers(l), 
                    () => SyntaxKind.PublicKeyword);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                node = (FieldDeclarationSyntax)EnsureVisibility(
                    node, 
                    node.Modifiers, 
                    (x, l) => x.WithModifiers(l), 
                    () => SyntaxKind.PrivateKeyword);

                // Now that the field has an explicit modifier remove any Dim modifiers on it 
                // as it is now redundant
                var list = node.Modifiers;
                var i = 0;
                while (i < list.Count)
                {
                    if (list[i].Kind() == SyntaxKind.DimKeyword)
                    {
                        list = list.RemoveAt(i);
                        break;
                    }

                    i++;
                }

                return node.WithModifiers(list);
            }

            public override SyntaxNode VisitDelegateStatement(DelegateStatementSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PublicKeyword);
            }

            public override SyntaxNode VisitPropertyStatement(PropertyStatementSyntax node)
            {
                if (IsInterfaceMember(node))
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), () => SyntaxKind.PublicKeyword);
            }

            private SyntaxKind GetTypeDefaultVisibility(TypeBlockSyntax originalTypeBlockSyntax)
            {
                // In the case of partial types we need to use the existing visibility if it exists
                if (originalTypeBlockSyntax.BlockStatement.Modifiers.Any(x => x.Kind() == SyntaxKind.PartialKeyword))
                {
                    SyntaxKind? kind = GetExistingPartialVisibility(originalTypeBlockSyntax);
                    if (kind.HasValue)
                    {
                        return kind.Value;
                    }
                }

                return GetDelegateTypeDefaultVisibility(originalTypeBlockSyntax);
            }

            private SyntaxKind GetDelegateTypeDefaultVisibility(SyntaxNode node)
            {
                return IsNestedDeclaration(node) ? SyntaxKind.PublicKeyword : SyntaxKind.FriendKeyword;
            }

            private SyntaxKind? GetExistingPartialVisibility(TypeBlockSyntax originalTypeBlockSyntax)
            {
                // Getting the SemanticModel is a relatively expensive operation.  Can take a few seconds in 
                // projects of significant size.  It is delay created to avoid this in files which already
                // conform to the standards.
                if (_semanticModel == null)
                {
                    _semanticModel = _document.GetSemanticModelAsync(_cancellationToken).Result;
                }

                var symbol = _semanticModel.GetDeclaredSymbol(originalTypeBlockSyntax, _cancellationToken);
                if (symbol == null)
                {
                    return null;
                }

                switch (symbol.DeclaredAccessibility)
                {
                    case Accessibility.Friend:
                        return SyntaxKind.FriendKeyword;
                    case Accessibility.Public:
                        return SyntaxKind.PublicKeyword;
                    case Accessibility.Private:
                        return SyntaxKind.PrivateKeyword;
                    case Accessibility.Protected:
                        return SyntaxKind.ProtectedKeyword;
                    default: return null;
                }
            }

            private static bool IsInterfaceMember(SyntaxNode node)
            {
                return node.Parent != null && node.Parent.Kind() == SyntaxKind.InterfaceBlock;
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
                    var kind = current.Kind();
                    if (kind == SyntaxKind.ClassBlock || kind == SyntaxKind.StructureBlock || kind == SyntaxKind.InterfaceBlock)
                    {
                        return true;
                    }

                    current = current.Parent;
                }

                return false;
            }

            private static SyntaxNode EnsureVisibility<T>(T node, SyntaxToken keyword, SyntaxTokenList originalModifiers, Func<T, SyntaxToken, T> withKeyword, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : SyntaxNode
            {
                Func<SyntaxKind, T> withFirstModifier = (visibilityKind) =>
                    {
                        var leadingTrivia = keyword.LeadingTrivia;
                        node = withKeyword(node, keyword.WithLeadingTrivia());

                        var visibilityToken = SyntaxFactory.Token(
                            leadingTrivia,
                            visibilityKind,
                            SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

                        var modifierList = SyntaxFactory.TokenList(visibilityToken);
                        return withModifiers(node, modifierList);
                    };

                return EnsureVisibilityCore(
                    node,
                    originalModifiers,
                    withFirstModifier,
                    withModifiers,
                    getDefaultVisibility);
            }

            private static SyntaxNode EnsureVisibility<T>(T node, SyntaxTokenList originalModifiers, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : SyntaxNode
            {
                Func<SyntaxKind, T> withFirstModifier = (visibilityKind) =>
                    {
                        var leadingTrivia = node.GetLeadingTrivia();
                        node = node.WithLeadingTrivia();

                        var visibilityToken = SyntaxFactory.Token(
                            leadingTrivia,
                            visibilityKind,
                            SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

                        var modifierList = SyntaxFactory.TokenList(visibilityToken);
                        return withModifiers(node, modifierList);
                    };

                return EnsureVisibilityCore(
                    node,
                    originalModifiers,
                    withFirstModifier,
                    withModifiers,
                    getDefaultVisibility);
            }

            /// <summary>
            /// Return a node declaration that has a visibility modifier.  If one isn't present it will be added as the 
            /// first modifier.  Any trivia before the node will be added as leading trivia to the added <see cref="SyntaxToken"/>.
            /// </summary>
            private static SyntaxNode EnsureVisibilityCore<T>(T node, SyntaxTokenList originalModifiers, Func<SyntaxKind, T> withFirstModifier, Func<T, SyntaxTokenList, T> withModifiers, Func<SyntaxKind> getDefaultVisibility) where T : SyntaxNode
            {
                if (originalModifiers.Any(x => SyntaxFacts.IsAccessibilityModifier(x.Kind())))
                {
                    return node;
                }

                SyntaxKind visibilityKind = getDefaultVisibility();
                Debug.Assert(SyntaxFacts.IsAccessibilityModifier(visibilityKind));

                SyntaxTokenList modifierList;
                if (originalModifiers.Count == 0)
                {
                    return withFirstModifier(visibilityKind);
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
                    return withModifiers(node, modifierList);
                }

            }
        }
    }
}
