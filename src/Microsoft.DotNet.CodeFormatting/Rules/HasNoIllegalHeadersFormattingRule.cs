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

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [Export(typeof(IFormattingRule))]
    [ExportMetadata("Order", 2)]
    internal sealed class HasNoIllegalHeadersFormattingRule : IFormattingRule
    {
        private static string[] IllegalHeaders = {"Copyright (c) Microsoft Corporation.", "<owner>", "</owner>", "==--==", "==++==", "<OWNER>", "</OWNER>" };
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
                return document;

            var leadingTrivia = syntaxNode.GetLeadingTrivia();
            IEnumerable<SyntaxTrivia> newTrivia = leadingTrivia;

            foreach (var trivia in leadingTrivia)
            {
                if (IllegalHeaders.Any(trivia.ToFullString().Contains))
                {
                    newTrivia = newTrivia.Where(t => t.ToFullString() != trivia.ToFullString());
                }
            }

            if (leadingTrivia.Equals(newTrivia))
                return document;

            return document.WithSyntaxRoot(syntaxNode.WithLeadingTrivia(newTrivia));
        }
    }
}
