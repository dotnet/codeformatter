// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    class ExplicitVariableTypeAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "DNS0003";
        internal const string VariableDeclarationCustomTag = "VariableDeclarationTag";
        internal const string ForEachStatementCustomTag = "ForEachStatementTag";

        private static readonly DiagnosticDescriptor s_ruleVariableDeclaration = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: VariableDeclarationCustomTag);
        private static readonly DiagnosticDescriptor s_ruleForEachStatement = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: ForEachStatementCustomTag);

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedRules = ImmutableArray.Create(s_ruleVariableDeclaration, s_ruleForEachStatement);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => s_supportedRules;

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var node = (VariableDeclarationSyntax)syntaxContext.Node;
                // Implicit typed variables cannot have multiple declartors  
                if (node.Type.IsVar &&
                    !IsTypeObvious(node) &&
                    !IsAnonymousType(node.Variables.Single(), syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleVariableDeclaration, node.GetLocation(), node.Variables.Single().Identifier.Text));
                }
            }, SyntaxKind.VariableDeclaration);

            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var node = (ForEachStatementSyntax)syntaxContext.Node; 
                if (node.Type.IsVar &&
                    !IsTypeObvious(node) &&
                    !IsAnonymousType(node, syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleForEachStatement, node.Identifier.GetLocation(), node.Identifier.Text));
                }
            }, SyntaxKind.ForEachStatement);
        }

        // Return true if given SyntaxNode has an anonymous type, which can't be replaced by an explicit type.
        private static bool IsAnonymousType(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            ISymbol symbol = model.GetDeclaredSymbol(node, cancellationToken); 
            return ((ILocalSymbol)symbol).Type.IsAnonymousType;
        }

        // Return true if given SyntaxNode is either VariableDeclarationSyntax or ForEachStatementSyntax and 
        // VariableDeclarationSyntax.Variables.Single().Initializer.Value or ForEachStatementSyntax.Expression is:
        //   1. LiteralExpressionSyntax, e.g. var x = 10;
        //   2. CastExpressionSyntax, e.g. var x = (Foo)f;
        //   3. A object creation syntax node, which (at least) includes:
        //          - ObjectCreationExpressionSyntax
        //          - ArrayCreationExpressionSyntax
        // 
        //      ImplicitArrayCreationExpressionSyntax: This one is not included. e.g. new[] {}
        //
        // TODO: AnonymousObjectCreationExpressionSyntax could be filtered out here as well, maybe we want to do that?
        //       The trade-off here is the logic we use to check syntax node is not as accurate as a query to SemanticModel, 
        //       which is (presumbly) much slower
        private static bool IsTypeObvious(SyntaxNode node)
        {
            
            if (node == null)
            {
                return false;
            }

            ExpressionSyntax expressionNode = null;
            if (node is VariableDeclarationSyntax)
            {
                expressionNode = ((VariableDeclarationSyntax)node).Variables.Single().Initializer.Value;
            }
            else if (node is ForEachStatementSyntax)
            {
                expressionNode = ((ForEachStatementSyntax)node).Expression;
            }
            return expressionNode != null &&
                   (expressionNode is LiteralExpressionSyntax ||
                    expressionNode is CastExpressionSyntax ||
                    expressionNode is ObjectCreationExpressionSyntax ||
                    expressionNode is ArrayCreationExpressionSyntax);
        }
    }
}
