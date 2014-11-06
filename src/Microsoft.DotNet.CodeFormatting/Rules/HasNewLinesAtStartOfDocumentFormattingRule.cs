// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
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
    [Export(typeof(IFormattingRule))]
    internal sealed class HasNewLinesAtStartOfDocumentFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxRoot == null)
                return document;
            IEnumerable<SyntaxNode> nodesToAdd = new List<SyntaxNode>();
            var firstUsing = syntaxRoot.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().First();
            var firstNamespace = syntaxRoot.DescendantNodesAndSelf().OfType<NamespaceDeclarationSyntax>().First();
            // NewLine between copyright and using directives.
            var newUsingTriviaList = !firstUsing.HasLeadingTrivia ? SyntaxTriviaList.Create(SyntaxFactory.EndOfLine("\n")) : SyntaxTriviaList.Empty;
            if (!newUsingTriviaList.Any())
            {
                newUsingTriviaList = !(firstUsing.GetLeadingTrivia().First().CSharpKind() == SyntaxKind.EndOfLineTrivia) ?
                firstUsing.GetLeadingTrivia().Insert(0, SyntaxFactory.EndOfLine("\n")) : SyntaxTriviaList.Empty;
                if (newUsingTriviaList.Any()) nodesToAdd = nodesToAdd.Concat(new[] { firstUsing });
            }

            // NewLine between copyright and using directives.
            var newNamespaceTriviaList = !firstNamespace.HasLeadingTrivia ? SyntaxTriviaList.Create(SyntaxFactory.EndOfLine("\n")) : SyntaxTriviaList.Empty;
            if (!newNamespaceTriviaList.Any())
            {
                newNamespaceTriviaList = !(firstNamespace.GetLeadingTrivia().First().CSharpKind() == SyntaxKind.EndOfLineTrivia) ?
                firstNamespace.GetLeadingTrivia().Insert(0, SyntaxFactory.EndOfLine("\n")) : SyntaxTriviaList.Empty;
                if (newNamespaceTriviaList.Any()) nodesToAdd = nodesToAdd.Concat(new[] { firstNamespace });
            }

            Func<SyntaxNode, SyntaxNode, SyntaxNode> replacementForNodes = (node, dummy) =>
            {
                if (node as NamespaceDeclarationSyntax != null)
                    return node.WithLeadingTrivia(newNamespaceTriviaList);
                if (node as UsingDirectiveSyntax != null)
                    return node.WithLeadingTrivia(newUsingTriviaList);

                return null;
            };
             
            if (nodesToAdd.Any())
            {
                return document.WithSyntaxRoot(syntaxRoot.ReplaceNodes(nodesToAdd, replacementForNodes));
            }

            return document;
        }
    }
}
