// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class ProvideExplicitVariableTypeFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(ProvideExplicitVariableTypeAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel model = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            SyntaxNode typeSyntaxNode = null;
            ILocalSymbol varSymbol = null;
            switch (GetRuleType(diagnostic))
            {
                case RuleType.RuleVariableDeclaration:
                    var variableDeclarationNode = root.
                        FindNode(diagnosticSpan).
                        FirstAncestorOrSelf<VariableDeclarationSyntax>();
                    Debug.Assert(variableDeclarationNode != null);
                    typeSyntaxNode = variableDeclarationNode?.Type;
                    // Implicitly typed variables cannot have multiple declarators (error situations should be filtered out by analyzer)
                    varSymbol = (ILocalSymbol)model.GetDeclaredSymbol(variableDeclarationNode.Variables.Single(), cancellationToken);
                    break;

                case RuleType.RuleForEachStatement:
                    var forEachStatementNode = root.
                        FindToken(diagnosticSpan.Start).
                        Parent?.
                        FirstAncestorOrSelf<ForEachStatementSyntax>();
                    Debug.Assert(forEachStatementNode != null);
                    typeSyntaxNode = forEachStatementNode?.Type;
                    varSymbol = model.GetDeclaredSymbol(forEachStatementNode, cancellationToken);
                    break;

                case RuleType.None:
                    break;
            }
            if (typeSyntaxNode!= null && varSymbol != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Resources.ExplicitVariableTypeFixer_Title,
                        c => ReplaceVarWithExplicitType(context.Document,
                                                        typeSyntaxNode, 
                                                        varSymbol.Type,
                                                        cancellationToken)),
                    diagnostic);
            }
        }

        private async Task<Document> ReplaceVarWithExplicitType(Document document, SyntaxNode varNode, ITypeSymbol explicitTypeSymbol, CancellationToken cancellationToken)
        {
            DocumentEditor documentEditor = await DocumentEditor.CreateAsync(document, cancellationToken);
            SyntaxNode explicitTypeNode = documentEditor.Generator.TypeExpression(explicitTypeSymbol)
                                          .WithAdditionalAnnotations(Simplifier.Annotation)
                                          .WithTriviaFrom(varNode);
            documentEditor.ReplaceNode(varNode, explicitTypeNode);
            return documentEditor.GetChangedDocument();
        }

        private static RuleType GetRuleType(Diagnostic diagnostic)
        {
            Debug.Assert(diagnostic != null);

            foreach (string customTag in diagnostic.Descriptor.CustomTags)
            {
                switch (customTag)
                {
                    case ProvideExplicitVariableTypeAnalyzer.VariableDeclarationCustomTag:
                        return RuleType.RuleVariableDeclaration;
                    case ProvideExplicitVariableTypeAnalyzer.ForEachStatementCustomTag:
                        return RuleType.RuleForEachStatement;
                    // Diagnostics corresponding to this fixer must have either one of these above two custom tags
                }
            }
            Debug.Fail("This program location is thought to be unreachable.");
            // This is just to make compiler happy
            return RuleType.None;
        }

        private enum RuleType
        {
            None = 0,
            RuleVariableDeclaration,
            RuleForEachStatement
        }
    }
}
