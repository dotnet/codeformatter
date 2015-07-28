// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExplicitThisAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "DNS0001";
        private static DiagnosticDescriptor s_rule = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitThisAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.ExplicitThisAnalyzer_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(s_rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var node = syntaxContext.Node as MemberAccessExpressionSyntax;

                if (node != null)
                {
                    if (node.Expression != null &&
                        node.Expression.Kind() == SyntaxKind.ThisExpression &&
                        IsPrivateField(node, syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                    {
                        syntaxContext.ReportDiagnostic(Diagnostic.Create(s_rule, node.GetLocation(), node.Name));
                    }
                }
            }, SyntaxKind.SimpleMemberAccessExpression);
        }

        private bool IsPrivateField(MemberAccessExpressionSyntax memberSyntax, SemanticModel model, CancellationToken token)
        {
            var symbolInfo = model.GetSymbolInfo(memberSyntax, token);
            if (symbolInfo.Symbol != null && symbolInfo.Symbol.Kind == SymbolKind.Field)
            {
                var field = (IFieldSymbol)symbolInfo.Symbol;
                return field.DeclaredAccessibility == Accessibility.Private;
            }

            return false;
        }
    }
}
