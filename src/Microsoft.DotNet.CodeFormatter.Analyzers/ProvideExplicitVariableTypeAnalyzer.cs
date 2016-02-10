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
using Microsoft.CodeAnalysis.Options;

using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ProvideExplicitVariableTypeAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = AnalyzerIds.ProvideExplicitVariableType;
        internal const string VariableDeclarationCustomTag = "VariableDeclarationTag";
        internal const string ForEachStatementCustomTag = "ForEachStatementTag";

        private static readonly DiagnosticDescriptor s_ruleVariableDeclaration = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: new[] { VariableDeclarationCustomTag, RuleType.LocalSemantic });
        private static readonly DiagnosticDescriptor s_ruleForEachStatement = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitVariableTypeAnalyzer_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: new[] { ForEachStatementCustomTag, RuleType.LocalSemantic });

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedRules = ImmutableArray.Create(s_ruleVariableDeclaration, s_ruleForEachStatement);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => s_supportedRules;

        public const string AnalyzerName = AnalyzerIds.ProvideExplicitVariableType + "." + nameof(AnalyzerIds.ProvideExplicitVariableType);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                if (!RuleEnabled(syntaxContext))
                {
                    return;
                }

                var node = (VariableDeclarationSyntax)syntaxContext.Node;
                var model = syntaxContext.SemanticModel;
                var token = syntaxContext.CancellationToken;
                // C# syntax doesn't allow implicitly typed variables to have multiple declarators,
                // but we need to handle error situations (incomplete or extra declarators).   
                if (node.Type != null &&
                    node.Type.IsVar &&
                    !IsTypeObvious(node) &&
                    !IsAnonymousType(node.Variables.FirstOrDefault(), model, token) &&
                    !HasErrors(node.Variables.FirstOrDefault(), model, token))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleVariableDeclaration, node.GetLocation(), node.Variables.Single().Identifier.Text));
                }
            }, SyntaxKind.VariableDeclaration);

            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                if (!RuleEnabled(syntaxContext))
                {
                    return;
                }

                var node = (ForEachStatementSyntax)syntaxContext.Node;
                var model = syntaxContext.SemanticModel;
                var token = syntaxContext.CancellationToken;
                if (node.Type != null &&
                    node.Identifier != null &&
                    node.Type.IsVar &&
                    !IsTypeObvious(node) &&
                    !IsAnonymousType(node, model, token)&&
                    !HasErrors(node, model, token))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleForEachStatement, node.Identifier.GetLocation(), node.Identifier.Text));
                }
            }, SyntaxKind.ForEachStatement);
        }

        private static bool HasErrors(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            ISymbol symbol = model.GetDeclaredSymbol(node, cancellationToken);
            SymbolKind? symbolKind = ((ILocalSymbol)symbol)?.Type?.Kind;
            return symbolKind.HasValue && symbolKind == SymbolKind.ErrorType;
        }

        private static bool RuleEnabled(SyntaxNodeAnalysisContext syntaxContext)
        {
            PropertyBag properties = OptionsHelper.GetProperties(syntaxContext.Options);

            return properties.GetProperty(
                OptionsHelper.BuildDefaultEnabledProperty(ProvideExplicitVariableTypeAnalyzer.AnalyzerName));
        }

        /// <summary>
        /// Returns true if given SyntaxNode has an anonymous type, which can't be replaced by an explicit type.
        /// </summary>
        private static bool IsAnonymousType(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            ISymbol symbol = model.GetDeclaredSymbol(node, cancellationToken);
            ITypeSymbol type = ((ILocalSymbol)symbol)?.Type;
            bool? isAnonymousType = type?.IsAnonymousType;
            bool? containsAnonymousTypeArguments = (type as INamedTypeSymbol)?.TypeArguments.Any(t => t.IsAnonymousType);
            return (isAnonymousType.HasValue && isAnonymousType.Value) ||
                   (containsAnonymousTypeArguments.HasValue && containsAnonymousTypeArguments.Value);
        }

        /// <summary>
        /// Returns true if:
        ///   1. given SyntaxNode is either VariableDeclarationSyntax or ForEachStatementSyntax and 
        ///      the variable is initialized with obvious type, OR,
        ///   2. the node under inspection has errors.
        /// 
        ///  We define "obvious" as one of the following:
        ///   1. LiteralExpressionSyntax, e.g. var x = 10;
        ///   2. CastExpressionSyntax, e.g. var x = (Foo)f;
        ///   3. BinaryExpressionSyntax with Kind == AsExpression
        ///   4. A object creation syntax node, which (at least) includes:
        ///      4.1 ObjectCreationExpressionSyntax
        ///      4.2 ArrayCreationExpressionSyntax
        /// 
        ///  ImplicitArrayCreationExpressionSyntax: This one is not included. e.g. new[] {}
        /// </summary>
        // TODO: AnonymousObjectCreationExpressionSyntax could be filtered out here as well, maybe we want to do that?
        //       The trade-off here is the logic we use to check syntax node is not as accurate as a query to SemanticModel, 
        //       which is (presumbly) much slower. 
        private static bool IsTypeObvious(SyntaxNode node)
        {   
            if (node == null)
            {
                return true;
            }
                                                   
            ExpressionSyntax expressionNode = node is VariableDeclarationSyntax ?
                                ((VariableDeclarationSyntax)node)?.Variables.FirstOrDefault()?.Initializer?.Value :
                                (node as ForEachStatementSyntax)?.Expression;

            return expressionNode == null ?
                   // 'expressionNode == null' means the code under inspection has errors,
                   // so we return 'true' to avoid firing a warning.
                   true :
                   // Check for obvious type.
                   expressionNode.Kind() == SyntaxKind.AsExpression ||
                   expressionNode is LiteralExpressionSyntax ||
                   expressionNode is CastExpressionSyntax ||
                   expressionNode is ObjectCreationExpressionSyntax ||
                   expressionNode is ArrayCreationExpressionSyntax ||
                   // Check if there's any missing node in the subtree.
                   // Since parser would generate the missing node (IsMissing == true) and attach a diagnostic to it,
                   // we traverse the subtree only if a diagnostic is attached as optimization.
                   (node.ContainsDiagnostics &&
                    node.DescendantNodesAndTokensAndSelf().Where(n => n.IsMissing).Any());
        }
    }
}
