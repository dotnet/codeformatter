// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    /// <summary>
    /// Mark any fields that can provably be marked as readonly.
    /// </summary>
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnwrittenWritableFieldAnalyzer: DiagnosticAnalyzer
    {
        private static DiagnosticDescriptor rule = new DiagnosticDescriptor("DNS0002",
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.UnwrittenWritableFieldAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.UnwrittenWritableFieldAnalyzer_MessageFormat)),
                                                                            "Usage",
                                                                            DiagnosticSeverity.Warning,
                                                                            true);

        private ISymbol _internalsVisibleToAttribute;

        // The set of fields which it will be safe to mark as readonly, if we discover
        // that they are never written to in this solution.
        private HashSet<IFieldSymbol> _candidateReadonlyFields;

        // The set of fields that are written to in this solution.
        private HashSet<IFieldSymbol> _writtenFields;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            _candidateReadonlyFields = new HashSet<IFieldSymbol>();
            _writtenFields = new HashSet<IFieldSymbol>();

            _internalsVisibleToAttribute = context.Compilation.GetTypeByMetadataName(
                                                "System.Runtime.CompilerServices.InternalsVisibleToAttribute");

            context.RegisterSymbolAction(EvaluateField, SymbolKind.Field);
            context.RegisterCompilationEndAction(ReportUnwrittenFields);
        }

        private void EvaluateField(SymbolAnalysisContext context)
        {
            var fieldSymbol = (IFieldSymbol)context.Symbol;
            if (fieldSymbol.IsCandidateReadonlyField(_internalsVisibleToAttribute))
            {
                _candidateReadonlyFields.Add(fieldSymbol);
            }
        }

        private void ReportUnwrittenFields(CompilationAnalysisContext context)
        {
            IEnumerable<IFieldSymbol> fieldsToMark = _candidateReadonlyFields.Except(_writtenFields);
            foreach (var field in fieldsToMark)
            {
                Diagnostic.Create(rule, field.Locations[0], field.Name);
            }
        }
    }

    internal static class FieldSymbolExtensions
    {
        private static readonly HashSet<string> s_serializingFieldAttributes = new HashSet<string>
        {
            "System.Composition.ImportAttribute",
            "System.Composition.ImportManyAttribute",
        };

        internal static bool IsCandidateReadonlyField(this IFieldSymbol fieldSymbol, ISymbol internalsVisibleToAttribute)
        {
            if (fieldSymbol.IsReadOnly || fieldSymbol.IsConst || fieldSymbol.IsExtern)
            {
                return false;
            }

            if (fieldSymbol.IsVisibleOutsideSolution(internalsVisibleToAttribute))
            {
                return false;
            }

            if (fieldSymbol.IsSerializableByAttributes())
            {
                return false;
            }

            return true;
        }

        private static bool IsSerializableByAttributes(this IFieldSymbol field)
        {
            return field.GetAttributes()
                .Any(attr => s_serializingFieldAttributes.Contains(NameHelper.GetFullName(attr.AttributeClass)));
        }
    }

    internal static class SymbolExtensions
    {
        internal static bool IsVisibleOutsideSolution(this ISymbol symbol, ISymbol internalsVisibleToAttribute)
        {
            Accessibility accessibility = symbol.DeclaredAccessibility;

            if (accessibility == Accessibility.NotApplicable)
            {
                if (symbol.Kind == SymbolKind.Field)
                {
                    accessibility = Accessibility.Private;
                }
                else
                {
                    accessibility = Accessibility.Internal;
                }
            }

            if (accessibility == Accessibility.Public || accessibility == Accessibility.Protected)
            {
                if (symbol.ContainingType != null)
                {
                    // A public symbol in a non-visible class isn't visible.
                    return IsVisibleOutsideSolution(symbol.ContainingType, internalsVisibleToAttribute);
                }

                // We can't mark it readonly because code outside the solution is allowed to
                // write to it.
                return true;
            }

            if (accessibility > Accessibility.Private)
            {
                bool visibleOutsideSolution = symbol.IsInAssemblyWhichExposesInternals(internalsVisibleToAttribute);

                if (visibleOutsideSolution)
                {
                    if (symbol.ContainingType != null)
                    {
                        // A visible symbol in a non-visible class isn't visible.
                        return symbol.ContainingType.IsVisibleOutsideSolution(internalsVisibleToAttribute);
                    }

                    // We can't mark it readonly because code outside the solution is allowed to
                    // write to it.
                    return true;
                }
            }

            return false;
        }

        private static bool IsInAssemblyWhichExposesInternals(
            this ISymbol field,
            ISymbol internalsVisibleToAttribute)
        {
            IAssemblySymbol assembly = field.ContainingAssembly;
            return assembly.GetAttributes().Any(a => Equals(a.AttributeClass, internalsVisibleToAttribute));
        }
    }
}
