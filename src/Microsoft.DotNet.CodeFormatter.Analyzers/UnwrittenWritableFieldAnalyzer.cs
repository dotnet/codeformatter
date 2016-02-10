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
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    /// <summary>
    /// Mark any fields that can provably be marked as readonly.
    /// </summary>
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnwrittenWritableFieldAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = AnalyzerIds.UnwrittenWritableField;
        private static DiagnosticDescriptor s_rule = new DiagnosticDescriptor(DiagnosticId,
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.UnwrittenWritableFieldAnalyzer_Title)),
                                                                            ResourceHelper.MakeLocalizableString(nameof(Resources.UnwrittenWritableFieldAnalyzer_MessageFormat)),
                                                                            "Usage",
                                                                            DiagnosticSeverity.Warning,
                                                                            true,
                                                                            customTags: RuleType.LocalSemantic);

        private static readonly SyntaxKind[] s_compoundAssignmentExpressionKinds =
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
                SyntaxKind.SubtractAssignmentExpression,
                SyntaxKind.PreIncrementExpression,
                SyntaxKind.PreDecrementExpression,
                SyntaxKind.PostIncrementExpression,
                SyntaxKind.PostDecrementExpression
            };

        private ISymbol _internalsVisibleToAttribute;

        // The set of fields which it will be safe to mark as readonly, if we discover
        // that they are never written to in this solution.
        private HashSet<IFieldSymbol> _candidateReadonlyFields;

        // The set of fields that are written to in this solution.
        private HashSet<IFieldSymbol> _writtenFields;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(s_rule);

        public const string AnalyzerName = AnalyzerIds.UnwrittenWritableField + "." + nameof(AnalyzerIds.UnwrittenWritableField);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            PropertyBag properties = OptionsHelper.GetProperties(context.Options);

            if (!properties.GetProperty(
                OptionsHelper.BuildDefaultEnabledProperty(UnwrittenWritableFieldAnalyzer.AnalyzerName)))
            {
                return;
            }

            _candidateReadonlyFields = new HashSet<IFieldSymbol>();
            _writtenFields = new HashSet<IFieldSymbol>();

            _internalsVisibleToAttribute = context.Compilation.GetTypeByMetadataName(
                                                "System.Runtime.CompilerServices.InternalsVisibleToAttribute");

            context.RegisterSymbolAction(LocateCandidateReadonlyFields, SymbolKind.Field);
            context.RegisterSyntaxNodeAction(CheckForAssignment, s_compoundAssignmentExpressionKinds);
            context.RegisterSyntaxNodeAction(CheckForRefOrOutParameter, SyntaxKind.Argument);
            context.RegisterSyntaxNodeAction(CheckForExternMethodWithRefParameters, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(CheckForExternIndexer, SyntaxKind.IndexerDeclaration);
            context.RegisterSyntaxNodeAction(CheckForInvocations, SyntaxKind.InvocationExpression);
            context.RegisterCompilationEndAction(ReportUnwrittenFields);
        }

        private void LocateCandidateReadonlyFields(SymbolAnalysisContext context)
        {
            var fieldSymbol = (IFieldSymbol)context.Symbol;
            if (fieldSymbol.IsCandidateReadonlyField(_internalsVisibleToAttribute))
            {
                _candidateReadonlyFields.Add(fieldSymbol);
            }
        }

        private void CheckForAssignment(SyntaxNodeAnalysisContext context)
        {
            ExpressionSyntax node = (context.Node as AssignmentExpressionSyntax)?.Left ?? 
                                    (context.Node as PrefixUnaryExpressionSyntax)?.Operand ??
                                    (context.Node as PostfixUnaryExpressionSyntax)?.Operand;
            CheckForFieldWrite(node, context.SemanticModel);
        } 

        private void CheckForRefOrOutParameter(SyntaxNodeAnalysisContext context)
        {
            var node = (ArgumentSyntax)context.Node;
            if (!node.RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                CheckForFieldWrite(node.Expression, context.SemanticModel);
            }
        }

        // An extern method that takes a ref parameter of a value type, or a parameter
        // of a reference type (whether or not it is marked with "ref"), might modify
        // any field of that type. We can't tell, because we can't see the method body.
        // So don't mark any fields of that type as readonly.
        private void CheckForExternMethodWithRefParameters(SyntaxNodeAnalysisContext context)
        {
            MethodDeclarationSyntax node = (MethodDeclarationSyntax)context.Node;
            if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.ExternKeyword)))
            {
                CheckForRefParameters(node.ParameterList.Parameters, context.SemanticModel);
            }
        }

        private void CheckForRefParameters(IEnumerable<ParameterSyntax> parameters, SemanticModel model)
        {
            foreach (ParameterSyntax parameter in parameters)
            {
                ITypeSymbol parameterType = model.GetTypeInfo(parameter.Type).Type;
                if (parameterType == null)
                {
                    continue;
                }

                bool canModify = true;
                if (parameterType.TypeKind == TypeKind.Struct)
                {
                    canModify = parameter.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword));
                }

                if (canModify)
                {
                    // This parameter might be used to modify one of the fields, since the
                    // implmentation is hidden from this analysys. Assume all fields
                    // of the type are written to
                    foreach (IFieldSymbol field in parameterType.GetMembers().OfType<IFieldSymbol>())
                    {
                        MarkFieldAsWritten(field);
                    }
                }
            }
        }

        private void CheckForExternIndexer(SyntaxNodeAnalysisContext context)
        {
            var node = (IndexerDeclarationSyntax)context.Node;
            if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.ExternKeyword)))
            {
                // This method body is unable to be analysed, so may contain writer instances
                CheckForRefParameters(node.ParameterList.Parameters, context.SemanticModel);
            }
        }

        // A call to myStruct.myField.myMethod() might change myField, since myMethod
        // might modify it. So those invocations need to be counted as writes.
        private void CheckForInvocations(SyntaxNodeAnalysisContext context)
        {
            var node = (InvocationExpressionSyntax)context.Node;
            if (node.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
                ISymbol symbol = context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
                if (symbol != null && symbol.Kind == SymbolKind.Field)
                {
                    var fieldSymbol = (IFieldSymbol)symbol;
                    if (fieldSymbol.Type.TypeKind == TypeKind.Struct)
                    {
                        if (!IsImmutablePrimitiveType(fieldSymbol.Type))
                        {
                            MarkFieldAsWritten(fieldSymbol);
                        }
                    }
                }
            }
        }

        private bool IsImmutablePrimitiveType(ITypeSymbol type)
        {
            // All of the "special type" structs exposed are all immutable,
            // so it's safe to assume all methods on them are non-mutating, and
            // therefore safe to call on a readonly field
            return type.SpecialType != SpecialType.None && type.TypeKind == TypeKind.Struct;
        }

        private void ReportUnwrittenFields(CompilationAnalysisContext context)
        {
            IEnumerable<IFieldSymbol> fieldsToMark = _candidateReadonlyFields.Except(_writtenFields);
            foreach (var field in fieldsToMark)
            {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, field.Locations[0], field.Name));
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

                MarkFieldAsWritten(fieldSymbol);
            }
        }

        private void MarkFieldAsWritten(IFieldSymbol field)
        {
            _writtenFields.Add(field);
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
