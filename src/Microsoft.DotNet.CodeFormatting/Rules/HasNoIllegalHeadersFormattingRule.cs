// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(2)]
    internal sealed class HasNoIllegalHeadersFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {

            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
                return document;

            var leadingTrivia = syntaxNode.GetLeadingTrivia();
            SyntaxTriviaList newTrivia = leadingTrivia;
            var illegalHeaders = GetIllegalHeaders();

            foreach (var trivia in leadingTrivia)
            {
                if (illegalHeaders.Any(s => trivia.ToFullString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    newTrivia = RemoveTrivia(newTrivia, trivia);
                }
            }

            if (leadingTrivia.Equals(newTrivia))
                return document;

            return document.WithSyntaxRoot(syntaxNode.WithLeadingTrivia(newTrivia));
        }

        private static HashSet<string> GetIllegalHeaders()
        {
            var filePath = Path.Combine(
                Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path)),
                "IllegalHeaders.md");

            if (!File.Exists(filePath))
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(File.ReadAllLines(filePath).Where(l => !l.StartsWith("##") && !l.Equals("")), StringComparer.OrdinalIgnoreCase);
        }

        private static SyntaxTriviaList RemoveTrivia(SyntaxTriviaList leadingTrivia, SyntaxTrivia trivia)
        {
            SyntaxTriviaList newTrivia = leadingTrivia;
            var index = leadingTrivia.IndexOf(trivia);
            if (leadingTrivia.ElementAt(index + 1).CSharpKind() == SyntaxKind.EndOfLineTrivia)
            {
                // Remove trivia
                newTrivia = newTrivia.RemoveAt(index);
                // Remove end of line after trivia
                newTrivia = newTrivia.RemoveAt(index);
            }
            else
            {
                // Remove trivia
                newTrivia = newTrivia.RemoveAt(index);
            }

            return newTrivia;
        }
    }
}
