// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExplicitVariableTypeAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = AnalyzerIds.ExplicitVariableType;
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
                // C# syntax doesn't allow implicitly typed variables have multiple declartors,
                // but we need to handle error situations (incomplete or extra declarators).   
                if (node.Type != null &&
                    node.Type.IsVar &&
                    !IsTypeObvious(node) &&
                    !IsAnonymousType(node.Variables.FirstOrDefault(), syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleVariableDeclaration, node.GetLocation(), node.Variables.Single().Identifier.Text));
                }
            }, SyntaxKind.VariableDeclaration);

            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var node = (ForEachStatementSyntax)syntaxContext.Node; 
                if (node.Type != null &&
                    node.Identifier != null &&
                    node.Type.IsVar &&
                    !IsTypeObvious(node) &&
                    !IsAnonymousType(node, syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleForEachStatement, node.Identifier.GetLocation(), node.Identifier.Text));
                }
            }, SyntaxKind.ForEachStatement);
        }

        /// <summary>
        /// Returns true if given SyntaxNode has an anonymous type, which can't be replaced by an explicit type.
        /// </summary>
        private static bool IsAnonymousType(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            ISymbol symbol = model.GetDeclaredSymbol(node, cancellationToken); 
            bool? isAnonymousType = ((ILocalSymbol)symbol)?.Type?.IsAnonymousType;
            return isAnonymousType.HasValue && isAnonymousType.Value;
        }

        /// <summary>
        /// Returns true if given SyntaxNode is either VariableDeclarationSyntax or ForEachStatementSyntax and 
        /// the variable is initialized with Non-obvious type.
        /// 
        ///  We define "non-obvious" as one of the following:
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
                return false;
            }

            ExpressionSyntax expressionNode = null;
            if (node is VariableDeclarationSyntax)
            {
                expressionNode = ((VariableDeclarationSyntax)node)?.Variables.FirstOrDefault()?.Initializer?.Value;
            }
            else if (node is ForEachStatementSyntax)
            {
                expressionNode = ((ForEachStatementSyntax)node)?.Expression;
            }
            else
            {
                Debug.Fail("This program location is thought to be unreachable.");
            }
            // 'expressionNode == null' means the code under inspection has errors,
            // so we return 'true' to avoid firing a warning.
            return expressionNode == null ?
                   true :
                   expressionNode != null &&
                 ((expressionNode is BinaryExpressionSyntax &&
                   expressionNode.Kind() == SyntaxKind.AsExpression) ||
                   expressionNode is LiteralExpressionSyntax ||
                   expressionNode is CastExpressionSyntax ||
                   expressionNode is ObjectCreationExpressionSyntax ||
                   expressionNode is ArrayCreationExpressionSyntax);
        }
    }
}
