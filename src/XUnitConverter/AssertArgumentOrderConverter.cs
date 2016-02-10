// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XUnitConverter
{
    public sealed class AssertArgumentOrderConverter : ConverterBase
    {
        private class Rewriter : CSharpSyntaxRewriter
        {
            private readonly SemanticModel _model;

            private static readonly HashSet<string> s_targetMethods =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "Xunit.Assert.Equal",
                    "Xunit.Assert.NotEqual",
                };

            private static readonly HashSet<string> s_actualParamterNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "actual",
                };

            private static readonly HashSet<string> s_expectedParamterNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "expected",
                };

            public Rewriter(SemanticModel model)
            {
                _model = model;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var symbol = _model.GetSymbolInfo(node.Expression).Symbol as IMethodSymbol;
                if (symbol == null || !s_targetMethods.Contains(NameHelper.GetFullName(symbol)))
                {
                    return base.VisitInvocationExpression(node);
                }

                int actualIndex = IndexOfParameterWithName(symbol, s_actualParamterNames);
                int expectedIndex = IndexOfParameterWithName(symbol, s_expectedParamterNames);

                if (actualIndex == -1 || expectedIndex == -1)
                {
                    return base.VisitInvocationExpression(node);
                }

                var argumentList = (ArgumentListSyntax)Visit(node.ArgumentList);

                if (!IsConstant(argumentList.Arguments[actualIndex].Expression) ||
                    IsConstant(argumentList.Arguments[expectedIndex].Expression))
                {
                    // Since the arguments are already walked, use them and visit the expression
                    return node.Update(
                        (ExpressionSyntax)Visit(node.Expression),
                        argumentList);
                }

                List<ArgumentSyntax> arguments = argumentList.Arguments.ToList();
                ArgumentSyntax actualArgument = arguments[actualIndex];
                ArgumentSyntax expectedArgument = arguments[expectedIndex];
                arguments[actualIndex] = expectedArgument;
                arguments[expectedIndex] = actualArgument;

                return node.Update(
                    (ExpressionSyntax)Visit(node.Expression),
                    argumentList.WithArguments(SyntaxFactory.SeparatedList(arguments)));
            }

            private bool IsConstant(ExpressionSyntax expression)
            {
                switch (expression.Kind())
                {
                    case SyntaxKind.CharacterLiteralExpression:
                    case SyntaxKind.FalseLiteralExpression:
                    case SyntaxKind.NullLiteralExpression:
                    case SyntaxKind.NumericLiteralExpression:
                    case SyntaxKind.StringLiteralExpression:
                    case SyntaxKind.TrueLiteralExpression:
                        {
                            return true;
                        }

                    case SyntaxKind.SimpleMemberAccessExpression:
                    case SyntaxKind.IdentifierName:
                        {
                            ISymbol symbol = _model.GetSymbolInfo(expression).Symbol;

                            if (symbol != null && symbol.Kind == SymbolKind.Field)
                            {
                                return ((IFieldSymbol)symbol).IsConst;
                            }

                            break;
                        }
                }

                return false;
            }

            private int IndexOfParameterWithName(IMethodSymbol symbol, HashSet<string> names)
            {
                for (int i = 0; i < symbol.Parameters.Length; i++)
                {
                    if (names.Contains(symbol.Parameters[i].Name))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        protected override async Task<Solution> ProcessAsync(
            Document document,
            SyntaxNode syntaxRoot,
            CancellationToken cancellationToken)
        {
            var rewriter = new Rewriter(await document.GetSemanticModelAsync(cancellationToken));
            var newNode = rewriter.Visit(syntaxRoot);

            return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newNode);
        }
    }
}
