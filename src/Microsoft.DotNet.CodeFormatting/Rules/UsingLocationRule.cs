// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    /// <summary>
    /// This will ensure that using directives are placed outside of the namespace.
    /// </summary>
    [SyntaxRule(UsingLocationRule.Name, UsingLocationRule.Description, SyntaxRuleOrder.UsingLocationFormattingRule, DefaultRule = false)]
    internal sealed class UsingLocationRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        internal const string Name = "UsingLocation";
        internal const string Description = "Place using directives outside namespace declarations";

        public SyntaxNode Process(SyntaxNode syntaxNode, string languageName)
        {
            var root = syntaxNode as CompilationUnitSyntax;
            if (root == null)
                return syntaxNode;

            // This rule can only be done safely as a syntax transformation when there is a single namespace
            // declaration in the file.  Once there is more than one it opens up the possibility of introducing
            // ambiguities to essentially make using directives global which were previously local.
            var namespaceDeclarationList = root.Members.OfType<NamespaceDeclarationSyntax>().ToList();
            if (namespaceDeclarationList.Count != 1)
            {
                return syntaxNode;
            }

            var namespaceDeclaration = namespaceDeclarationList.Single();
            var usingList = namespaceDeclaration.Usings;
            if (usingList.Count == 0)
            {
                return syntaxNode;
            }

            // Moving a using with an alias out of a namespace is an operation which requires
            // semantic knowledge to get correct.
            if (usingList.Any(x => x.Alias != null))
            {
                return syntaxNode;
            }

            // We don't have the capability to safely move usings which are embedded inside an #if
            // directive.  
            //
            //  #if COND
            //  using NS1;
            //  #endif
            //
            // At the time there isn't a great way (that we know of) for detecting this particular 
            // case.  Instead we simply don't do this rewrite if the file contains any #if directives.
            if (root.DescendantTrivia().Any(x => x.Kind() == SyntaxKind.IfDirectiveTrivia))
            {
                return syntaxNode;
            }

            // We need to insert the new usings between the namespace and its leading trivia otherwise this rule incorrectly inserts the usings before the copyright headers.
            var newRoot = root;
            var leadingTrivia = namespaceDeclaration.GetLeadingTrivia();
            var newNamespace = namespaceDeclaration
                .WithUsings(SyntaxFactory.List<UsingDirectiveSyntax>())
                .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            var additionalUsings = namespaceDeclaration.Usings;
            var firstUsing = additionalUsings[0];
            var newFirstUsing = firstUsing.WithLeadingTrivia(leadingTrivia.Concat(firstUsing.GetLeadingTrivia()));
            additionalUsings = additionalUsings.Replace(firstUsing, newFirstUsing);
            newRoot = newRoot.ReplaceNode(namespaceDeclaration, newNamespace);
            newRoot = newRoot.WithUsings(newRoot.Usings.AddRange(additionalUsings));

            return newRoot;
        }
    }
}
