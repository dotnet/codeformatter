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
    [RuleOrder(12)]
    internal sealed class ExplicitVisibilityRule : IFormattingRule
    {
        private sealed class VisibilityRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
                var visibilityKind = GetTypeDefaultVisibility(node);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), visibilityKind);
            }

            public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
            {
                var visibilityKind = GetTypeDefaultVisibility(node);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), visibilityKind);
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                node = (StructDeclarationSyntax)base.VisitStructDeclaration(node);
                var visibilityKind = GetTypeDefaultVisibility(node);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), visibilityKind);
            }

            public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
            {
                var visibilityKind = GetTypeDefaultVisibility(node);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), visibilityKind);
            }

            public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
            {
                var visibilityKind = GetTypeDefaultVisibility(node);
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), visibilityKind);
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                if (node.Modifiers.Any(x => x.CSharpKind() == SyntaxKind.StaticKeyword))
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (node.ExplicitInterfaceSpecifier != null)
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (node.ExplicitInterfaceSpecifier != null)
                {
                    return node;
                }

                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), SyntaxKind.PrivateKeyword);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                return EnsureVisibility(node, node.Modifiers, (x, l) => x.WithModifiers(l), SyntaxKind.PrivateKeyword);
            }

            private static SyntaxKind GetTypeDefaultVisibility(SyntaxNode node)
            {
                return IsNestedDeclaration(node) ? SyntaxKind.PrivateKeyword : SyntaxKind.InternalKeyword;
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
            private static MemberDeclarationSyntax EnsureVisibility<T>(T node, SyntaxTokenList originalModifiers, Func<T, SyntaxTokenList, T> withModifiers, SyntaxKind visibilityKind) where T : MemberDeclarationSyntax
            {
                Debug.Assert(SyntaxFacts.IsAccessibilityModifier(visibilityKind));

                if (originalModifiers.Any(x => SyntaxFacts.IsAccessibilityModifier(x.CSharpKind())))
                {
                    return node;
                }

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

        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
            {
                return document;
            }

            var rewriter = new VisibilityRewriter();
            var newNode = rewriter.Visit(syntaxNode);
            return document.WithSyntaxRoot(newNode);
        }
    }
}
