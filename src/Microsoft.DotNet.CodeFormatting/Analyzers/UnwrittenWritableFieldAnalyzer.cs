// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    /// <summary>
    /// Mark any fields that can provably be marked as readonly.
    /// </summary>
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnwrittenWritableFieldAnalyzer: DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "DNS0002";
        private static DiagnosticDescriptor rule = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.UnwrittenWritableFieldAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.UnwrittenWritableFieldAnalyzer_MessageFormat)),
                                                                            "Usage",
                                                                            DiagnosticSeverity.Warning,
                                                                            true);

        private static readonly SyntaxKind[] compoundAssignmentExpressionKinds =
            {
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.AddAssignmentExpression,
                SyntaxKind.AndAssignmentExpression,
                SyntaxKind.DivideAssignmentExpression,
                SyntaxKind.ExclusiveOrAssignmentExpression,
                SyntaxKind.LeftShiftAssignmentExpression,
                SyntaxKind.ModuloAssignmentExpression,
                SyntaxKind.MultiplyAssignmentExpression,
                SyntaxKind.OrAssignmentExpression,
                SyntaxKind.RightShiftAssignmentExpression,
                SyntaxKind.SubtractAssignmentExpression
            };

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
            context.RegisterSyntaxNodeAction(CheckForAssignment, compoundAssignmentExpressionKinds);
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

        private void CheckForAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignmentExpression = (AssignmentExpressionSyntax)context.Node;
            CheckForFieldWrite(assignmentExpression.Left, context.SemanticModel);
        }

        private void ReportUnwrittenFields(CompilationAnalysisContext context)
        {
            IEnumerable<IFieldSymbol> fieldsToMark = _candidateReadonlyFields.Except(_writtenFields);
            foreach (var field in fieldsToMark)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, field.Locations[0], field.Name));
            }
        }

        private void CheckForFieldWrite(ExpressionSyntax node, SemanticModel model)
        {
            var fieldSymbol = model.GetSymbolInfo(node).Symbol as IFieldSymbol;

            if (fieldSymbol != null)
            {
                if (IsInsideOwnConstructor(node, fieldSymbol.ContainingType, fieldSymbol.IsStatic, model))
                {
                    return;
                }

                _writtenFields.Add(fieldSymbol);
            }
        }

        private bool IsInsideOwnConstructor(SyntaxNode node, ITypeSymbol type, bool isStatic, SemanticModel model)
        {
            while (node != null)
            {
                switch (node.Kind())
                {
                    case SyntaxKind.ConstructorDeclaration:
                        {
                            return model.GetDeclaredSymbol(node).IsStatic == isStatic &&
                                IsInType(node.Parent, type, model);
                        }
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.AnonymousMethodExpression:
                        return false;
                }

                node = node.Parent;
            }

            return false;
        }

        private bool IsInType(SyntaxNode node, ITypeSymbol containingType, SemanticModel model)
        {
            while (node != null)
            {
                if (node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration))
                {
                    return Equals(containingType, model.GetDeclaredSymbol(node));
                }

                node = node.Parent;
            }

            return false;
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
