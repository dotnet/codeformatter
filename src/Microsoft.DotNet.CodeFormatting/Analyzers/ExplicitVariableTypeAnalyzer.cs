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
                if (node.Type.IsVar && !IsAnonymousType(node.Variables.Single(), syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                {
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(s_ruleVariableDeclaration, node.GetLocation(), node.Variables.Single().Identifier.Text));
                }
            }, SyntaxKind.VariableDeclaration);

            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var node = (ForEachStatementSyntax)syntaxContext.Node; 
                if (node.Type.IsVar && !IsAnonymousType(node, syntaxContext.SemanticModel, syntaxContext.CancellationToken))
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
    }
}
