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

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(9)]
    internal sealed class HasPrivateAccessorOnFieldNames : IFormattingRule
    {
        private static readonly string[] AccessorModifiers = {"public", "private", "internal", "protected"};
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;

            var typeNodes = syntaxRoot.DescendantNodes()
                .Where(type => type.CSharpKind() == SyntaxKind.ClassDeclaration || type.CSharpKind() == SyntaxKind.StructDeclaration);
            var fieldsWithNoAccessor = GetFieldsWithNoAccessors(typeNodes);

            Func<SyntaxNode, SyntaxNode, SyntaxNode> addPrivateToFields = (node, dummy) =>
            {
                var field = node as FieldDeclarationSyntax;
                IEnumerable<SyntaxToken> tokenList = Enumerable.Empty<SyntaxToken>();
                var privateKeyword = SyntaxFactory.Token(SyntaxKind.PrivateKeyword);
                if (field.Modifiers.Any())
                {
                    // There is trivia associated with the first modifier token
                    privateKeyword = privateKeyword.WithLeadingTrivia(field.Modifiers.First().LeadingTrivia);
                    var nextTrivia = field.Modifiers.First().WithLeadingTrivia();
                    tokenList = tokenList.Concat(new[] { privateKeyword, nextTrivia}).Concat(field.Modifiers.Skip(1));
                }
                else if (field.ChildNodes().OfType<VariableDeclarationSyntax>().First().GetFirstToken().HasLeadingTrivia)
                {
                    // There is trivia association with the first token - not a modifier token
                    var firstToken = field.ChildNodes().OfType<VariableDeclarationSyntax>().First().GetFirstToken();
                    privateKeyword = privateKeyword.WithLeadingTrivia(firstToken.LeadingTrivia);
                    field = field.ReplaceToken(firstToken, firstToken.WithLeadingTrivia());
                    tokenList = tokenList.Concat(new[] { privateKeyword });
                }
                else
                {
                    tokenList = tokenList.Concat(new[] { privateKeyword });
                }

                return field.WithModifiers(SyntaxFactory.TokenList(tokenList));
            };

            return document.WithSyntaxRoot(syntaxRoot.ReplaceNodes(fieldsWithNoAccessor, addPrivateToFields));
        }

        private IEnumerable<FieldDeclarationSyntax> GetFieldsWithNoAccessors(IEnumerable<SyntaxNode> typeNodes)
        {
            IEnumerable<FieldDeclarationSyntax> fieldsWithNoAccessor = Enumerable.Empty<FieldDeclarationSyntax>();
            foreach (var type in typeNodes)
            {
                fieldsWithNoAccessor = fieldsWithNoAccessor.Concat(type.ChildNodes().OfType<FieldDeclarationSyntax>().Where(f => !AccessorModifiers.Any(f.Modifiers.ToString().Contains)));
            }

            return fieldsWithNoAccessor;
        }
    }
}
