// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    /// <summary>
    /// Mark any fields that can provably be marked as readonly.
    /// </summary>
    [GlobalSemanticRule(MarkReadonlyFieldsRule.Name, MarkReadonlyFieldsRule.Description, GlobalSemanticRuleOrder.MarkReadonlyFieldsRule, DefaultRule = false)]
    internal sealed class MarkReadonlyFieldsRule : IGlobalSemanticFormattingRule
    {
        internal const string Name = "ReadonlyFields";
        internal const string Description = "Mark fields which can be readonly as readonly";

        private readonly SemaphoreSlim _processUsagesLock = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<IFieldSymbol, bool> _unwrittenWritableFields;

        public bool SupportsLanguage(string languageName)
        {
            return languageName == LanguageNames.CSharp;
        }

        public async Task<Solution> ProcessAsync(
            Document document,
            SyntaxNode syntaxRoot,
            CancellationToken cancellationToken)
        {
            if (_unwrittenWritableFields == null)
            {
                using (await SemaphoreLock.GetAsync(_processUsagesLock))
                {
                    // A global analysis must be run before we can do any actual processing, because a field might
                    // be written in a different file than it is declared (even private ones may be split between
                    // partial classes).

                    // It's also quite expensive, which is why it's being done inside the lock, so
                    // that the entire solution is not processed for each input file individually
                    if (_unwrittenWritableFields == null)
                    {
                        List<Document> allDocuments =
                            document.Project.Solution.Projects.SelectMany(p => p.Documents).ToList();
                        HashSet<IFieldSymbol>[] fields = await Task.WhenAll(
                            allDocuments
                                .AsParallel()
                                .Select(
                                    doc => WritableFieldScanner.Scan(doc, cancellationToken)));

                        var writableFields = new ConcurrentDictionary<IFieldSymbol, bool>(
                            fields.SelectMany(s => s).Select(f => new KeyValuePair<IFieldSymbol, bool>(f, true)));

                        await Task.WhenAll(
                            allDocuments.AsParallel()
                                .Select(
                                    doc => WriteUsagesScanner.RemoveWrittenFields(
                                        doc,
                                        writableFields,
                                        cancellationToken)));

                        _unwrittenWritableFields = writableFields;
                    }
                }
            }

            if (_unwrittenWritableFields.Count == 0)
            {
                // If there are no unwritten writable fields, skip all the rewriting
                return document.Project.Solution;
            }

            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken);
            var application = new ReadonlyRewriter(
                _unwrittenWritableFields,
                await document.GetSemanticModelAsync(cancellationToken));
            return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, application.Visit(root));
        }

        /// <summary>
        /// This is the first walker, which looks for fields that are valid to transform to readonly.
        /// It returns any private or internal fields that are not already marked readonly, and returns a hash set
        /// of them. Internal fields are only considered if the "InternalsVisibleTo" is a reference to something
        /// in the same solution, since it's possible to analyse the global usages of it. Otherwise there is an
        /// assembly we don't have access to that can see that field, so we have to treat is as public.
        /// </summary>
        private sealed class WritableFieldScanner : CSharpSyntaxWalker
        {
            private static readonly HashSet<string> s_serializingFieldAttributes = new HashSet<string>
            {
                "System.ComponentModel.Composition.ImportAttribute",
                "System.ComponentModel.Composition.ImportManyAttribute",
            };

            private readonly HashSet<IFieldSymbol> _fields = new HashSet<IFieldSymbol>();
            private readonly ISymbol _internalsVisibleToAttribute;
            private readonly SemanticModel _model;

            private WritableFieldScanner(SemanticModel model)
            {
                _model = model;
                _internalsVisibleToAttribute =
                    model.Compilation.GetTypeByMetadataName(
                        "System.Runtime.CompilerServices.InternalsVisibleToAttribute");
            }

            public static async Task<HashSet<IFieldSymbol>> Scan(
                Document document,
                CancellationToken cancellationToken)
            {
                var scanner = new WritableFieldScanner(
                    await document.GetSemanticModelAsync(cancellationToken));
                scanner.Visit(await document.GetSyntaxRootAsync(cancellationToken));
                return scanner._fields;
            }

            public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                var fieldSymbol = (IFieldSymbol)_model.GetDeclaredSymbol(node.Declaration.Variables[0]);

                if (fieldSymbol.IsReadOnly || fieldSymbol.IsConst || fieldSymbol.IsExtern)
                {
                    return;
                }

                if (IsSymbolVisibleOutsideSolution(fieldSymbol, _internalsVisibleToAttribute))
                {
                    return;
                }

                if (IsFieldSerializableByAttributes(fieldSymbol))
                {
                    return;
                }

                _fields.Add(fieldSymbol);
            }

            private static bool IsSymbolVisibleOutsideSolution(ISymbol symbol, ISymbol internalsVisibleToAttribute)
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
                        // a public symbol in a non-visible class isn't visible
                        return IsSymbolVisibleOutsideSolution(symbol.ContainingType, internalsVisibleToAttribute);
                    }

                    // They are public, we are going to skip them.
                    return true;
                }

                if (accessibility > Accessibility.Private)
                {
                    bool visibleOutsideSolution = IsVisibleOutsideSolution(
                        symbol,
                        internalsVisibleToAttribute);

                    if (visibleOutsideSolution)
                    {
                        if (symbol.ContainingType != null)
                        {
                            // a visible symbol in a non-visible class isn't visible
                            return IsSymbolVisibleOutsideSolution(
                                symbol.ContainingType,
                                internalsVisibleToAttribute);
                        }

                        return true;
                    }
                }

                return false;
            }

            private static bool IsVisibleOutsideSolution(
                ISymbol field,
                ISymbol internalsVisibleToAttribute)
            {
                IAssemblySymbol assembly = field.ContainingAssembly;
                return assembly.GetAttributes().Any(a => Equals(a.AttributeClass, internalsVisibleToAttribute));
            }

            private bool IsFieldSerializableByAttributes(IFieldSymbol field)
            {
                if (field.GetAttributes()
                    .Any(attr => s_serializingFieldAttributes.Contains(NameHelper.GetFullName(attr.AttributeClass))))
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// This is the second walker. It checks all code for instances where one of the writable fields (as
        /// calculated by <see cref="WritableFieldScanner" />) is written to, and removes it from the set.
        /// Once the scan is complete, the set will not contain any fields written in the specified document.
        /// </summary>
        private sealed class WriteUsagesScanner : CSharpSyntaxWalker
        {
            private readonly SemanticModel _semanticModel;
            private readonly ConcurrentDictionary<IFieldSymbol, bool> _writableFields;

            private WriteUsagesScanner(
                SemanticModel semanticModel,
                ConcurrentDictionary<IFieldSymbol, bool> writableFields)
            {
                _semanticModel = semanticModel;
                _writableFields = writableFields;
            }

            public override void VisitArgument(ArgumentSyntax node)
            {
                base.VisitArgument(node);

                if (!node.RefOrOutKeyword.IsKind(SyntaxKind.None))
                {
                    CheckForFieldWrite(node.Expression);
                }
            }

            public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                base.VisitAssignmentExpression(node);

                CheckForFieldWrite(node.Left);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                base.VisitBinaryExpression(node);
                switch (node.OperatorToken.Kind())
                {
                    case SyntaxKind.AddAssignmentExpression:
                    case SyntaxKind.AndAssignmentExpression:
                    case SyntaxKind.DivideAssignmentExpression:
                    case SyntaxKind.ExclusiveOrAssignmentExpression:
                    case SyntaxKind.LeftShiftAssignmentExpression:
                    case SyntaxKind.ModuloAssignmentExpression:
                    case SyntaxKind.MultiplyAssignmentExpression:
                    case SyntaxKind.OrAssignmentExpression:
                    case SyntaxKind.RightShiftAssignmentExpression:
                    case SyntaxKind.SubtractAssignmentExpression:
                        {
                            CheckForFieldWrite(node.Left);
                            break;
                        }
                }
            }

            public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
            {
                base.VisitIndexerDeclaration(node);

                if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.ExternKeyword)))
                {
                    // This method body is unable to be analysed, so may contain writer instances
                    CheckForRefParametersForExternMethod(node.ParameterList.Parameters);
                }
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                base.VisitInvocationExpression(node);

                // A call to myStruct.myField.myMethod() will change if "myField" is marked
                // readonly, since myMethod might modify it. So those need to be counted as writes

                if (node.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
                    ISymbol symbol = _semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
                    if (symbol != null && symbol.Kind == SymbolKind.Field)
                    {
                        var fieldSymbol = (IFieldSymbol)symbol;
                        if (fieldSymbol.Type.TypeKind == TypeKind.Struct)
                        {
                            if (!IsImmutablePrimitiveType(fieldSymbol.Type))
                            {
                                MarkWriteInstance(fieldSymbol);
                            }
                        }
                    }
                }
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                base.VisitMethodDeclaration(node);

                if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.ExternKeyword)))
                {
                    // This method body is unable to be analysed, so may contain writer instances
                    CheckForRefParametersForExternMethod(node.ParameterList.Parameters);
                }
            }

            private void CheckForRefParametersForExternMethod(IEnumerable<ParameterSyntax> parameters)
            {
                foreach (ParameterSyntax parameter in parameters)
                {
                    ITypeSymbol parameterType = _semanticModel.GetTypeInfo(parameter.Type).Type;
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
                            MarkWriteInstance(field);
                        }
                    }
                }
            }

            private void CheckForFieldWrite(ExpressionSyntax node)
            {
                var fieldSymbol = _semanticModel.GetSymbolInfo(node).Symbol as IFieldSymbol;

                if (fieldSymbol != null)
                {
                    if (IsInsideOwnConstructor(node, fieldSymbol.ContainingType, fieldSymbol.IsStatic))
                    {
                        return;
                    }

                    MarkWriteInstance(fieldSymbol);
                }
            }

            private bool IsImmutablePrimitiveType(ITypeSymbol type)
            {
                // All of the "special type" structs exposed are all immutable,
                // so it's safe to assume all methods on them are non-mutating, and
                // therefore safe to call on a readonly field
                return type.SpecialType != SpecialType.None && type.TypeKind == TypeKind.Struct;
            }

            private bool IsInsideOwnConstructor(SyntaxNode node, ITypeSymbol type, bool isStatic)
            {
                while (node != null)
                {
                    switch (node.Kind())
                    {
                        case SyntaxKind.ConstructorDeclaration:
                            {
                                return _semanticModel.GetDeclaredSymbol(node).IsStatic == isStatic &&
                                    IsInType(node.Parent, type);
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

            private bool IsInType(SyntaxNode node, ITypeSymbol containingType)
            {
                while (node != null)
                {
                    if (node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration))
                    {
                        return Equals(containingType, _semanticModel.GetDeclaredSymbol(node));
                    }

                    node = node.Parent;
                }
                return false;
            }

            private void MarkWriteInstance(IFieldSymbol fieldSymbol)
            {
                bool ignored;
                _writableFields.TryRemove(fieldSymbol, out ignored);
            }

            public static async Task RemoveWrittenFields(
                Document document,
                ConcurrentDictionary<IFieldSymbol, bool> writableFields,
                CancellationToken cancellationToken)
            {
                var scanner = new WriteUsagesScanner(
                    await document.GetSemanticModelAsync(cancellationToken),
                    writableFields);
                scanner.Visit(await document.GetSyntaxRootAsync(cancellationToken));
            }
        }

        /// <summary>
        /// This is the actually rewriter, and should be run third, using the data gathered from the other two
        /// (<see cref="WritableFieldScanner" /> and <see cref="WriteUsagesScanner" />).
        /// Any field in the set is both writeable, but not actually written to, which means the "readonly"
        /// modifier should be applied to it.
        /// </summary>
        private sealed class ReadonlyRewriter : CSharpSyntaxRewriter
        {
            private readonly SemanticModel _model;
            private readonly ConcurrentDictionary<IFieldSymbol, bool> _unwrittenFields;

            public ReadonlyRewriter(ConcurrentDictionary<IFieldSymbol, bool> unwrittenFields, SemanticModel model)
            {
                _model = model;
                _unwrittenFields = unwrittenFields;
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                var fieldSymbol = (IFieldSymbol)_model.GetDeclaredSymbol(node.Declaration.Variables[0]);
                bool ignored;
                if (_unwrittenFields.TryRemove(fieldSymbol, out ignored))
                {
                    return
                        node.WithModifiers(
                            node.Modifiers.Add(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.ReadOnlyKeyword,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")))));
                }

                return node;
            }
        }
    }
}
