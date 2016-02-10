// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
    public class OptimizeNamespaceImportsAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = AnalyzerIds.OptimizeNamespaceImports;
        private static DiagnosticDescriptor s_rule = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.OptimizeNamespaceImportsAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.OptimizeNamespaceImportsAnalyzer_MessageFormat)),
                                                                            "Style",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: RuleType.GlobalSemantic);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(s_rule);

        public const string AnalyzerName = AnalyzerIds.OptimizeNamespaceImports + "." + nameof(AnalyzerIds.OptimizeNamespaceImports);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                PropertyBag properties = OptionsHelper.GetProperties(compilationContext.Options);

                if (!properties.GetProperty(
                    OptionsHelper.BuildDefaultEnabledProperty(OptimizeNamespaceImportsAnalyzer.AnalyzerName)))
                {
                    // Analyzer is entirely disabled
                    return;
                }

                if (properties.GetProperty(OptimizeNamespaceImportsOptions.RemoveUnnecessaryImports))
                {
                    context.RegisterSemanticModelAction(LookForUnusedImports);
                }
            });
        }

        private static void LookForUnusedImports(SemanticModelAnalysisContext semanticModelAnalysisContext)
        {
            SyntaxNode root;
            SemanticModel semanticModel;

            semanticModel = semanticModelAnalysisContext.SemanticModel;
            root = semanticModel.SyntaxTree.GetRoot();

            // If we encounter any conditionally included code, we cannot be sure
            // that unused namespaces might not be relevant for some other compilation
            if (root.DescendantTrivia().Any(x => x.Kind() == SyntaxKind.IfDirectiveTrivia))
                return;

            var diagnostics = semanticModel.GetDiagnostics(null, semanticModelAnalysisContext.CancellationToken);
            Diagnostic firstDiagnostic = null;
            var locations = new List<Location>();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Id == "CS8019")
                {
                    firstDiagnostic = firstDiagnostic ?? diagnostic;
                    locations.Add(diagnostic.Location);
                }
            }

            if (locations.Count > 0)
            {
                semanticModelAnalysisContext.ReportDiagnostic(Diagnostic.Create(s_rule, firstDiagnostic.Location, locations));
            }
        }
    }
}
