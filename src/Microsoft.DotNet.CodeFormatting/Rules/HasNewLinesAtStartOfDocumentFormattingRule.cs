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
            Document newDocument;
            // NewLine between copyright and using directives.
            var firstUsing = syntaxRoot.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().First();
            var hasEndOfLineTrivia = firstUsing.GetLeadingTrivia().First().CSharpKind() == SyntaxKind.EndOfLineTrivia;
            if (!hasEndOfLineTrivia)
            {
                var newTriviaList = firstUsing.GetLeadingTrivia().Insert(0, SyntaxFactory.EndOfLine("\n"));
                newDocument =  document.WithSyntaxRoot(syntaxRoot.ReplaceNode(firstUsing, firstUsing.WithLeadingTrivia(newTriviaList)));
            }

            return document;
        }
    }
}
