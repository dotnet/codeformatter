// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [Export(typeof(IFormattingRule))]
    internal sealed class IsFormattedFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            // Roslyn formatter doesn't format code in #if false as it's considered as DisabledTextTrivia. Will be removed after the bug is fixed.
            // Doing that manually here
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            var oldTrivia = syntaxRoot.DescendantTrivia().Where(trivia => trivia.CSharpKind() == SyntaxKind.DisabledTextTrivia);
            Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia> replacementTrivia = (trivia, dummy) =>
            {
                var levelToIndent = trivia.Token.Parent.Ancestors().Count();
                var compilation = SyntaxFactory.ParseCompilationUnit(trivia.ToString());
                var formattedTrivia = Formatter.Format(compilation.SyntaxTree.GetRoot(), document.Project.Solution.Workspace).GetText().ToString();
                return SyntaxFactory.DisabledText(formattedTrivia);
            };

            var newDocument = document.WithSyntaxRoot(syntaxRoot.ReplaceTrivia(oldTrivia, replacementTrivia));
            return await Formatter.FormatAsync(newDocument, cancellationToken: cancellationToken);
        }
    }
}