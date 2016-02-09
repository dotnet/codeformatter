// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PlaceImportsOutsideNamespaceAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = AnalyzerIds.PlaceImportsOutsideNamespace;
        private static DiagnosticDescriptor s_rule = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.PlaceImportsOutsideNamespace_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.PlaceImportsOutsideNamespace_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: RuleType.LocalSemantic);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

        public const string AnalyzerName = AnalyzerIds.PlaceImportsOutsideNamespace + "." + nameof(AnalyzerIds.PlaceImportsOutsideNamespace);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                PropertyBag properties = OptionsHelper.GetProperties(compilationContext.Options);

                if (!properties.GetProperty(OptionsHelper.BuildDefaultEnabledProperty(AnalyzerName)))
                {
                    // Analyzer is entirely disabled
                    return;
                }
                
                if (properties.GetProperty(OptimizeNamespaceImportsOptions.PlaceImportsOutsideNamespaceDeclaration))
                {
                    context.RegisterSyntaxNodeAction(LookForUsingsInsideNamespace, SyntaxKind.NamespaceDeclaration);
                }
            });
        }

        private static void LookForUsingsInsideNamespace(SyntaxNodeAnalysisContext syntaxContext)
        {
            var namespaceDeclaration = syntaxContext.Node as NamespaceDeclarationSyntax;
            if (namespaceDeclaration.Usings.Count != 0)
            {
                var allLocations = namespaceDeclaration.Usings.Select(d => d.GetLocation());
                syntaxContext.ReportDiagnostic(Diagnostic.Create(s_rule, namespaceDeclaration.Usings.First().GetLocation(), allLocations));
            }
        }
    }
}
