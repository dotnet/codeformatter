// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    /// <summary>
    /// Regions with expressions which evaluate to "varying" will not be removed by the analysis engine.
    /// Such expressions can be simplified by collapsing binary expressions of the forms:
    /// 
    /// true && varying
    /// false || varying
    /// 
    /// to "varying".
    /// 
    /// When removing all references to a given preprocessor symbol which has a known value, the symbol
    /// in question may also happen to appear in expressions which evaluate to varying. By simplifying
    /// such expressions, we alleviate the need to do any manual clean up of references to the symbol
    /// after removing dead conditional regions.
    /// </summary>
    internal class PreprocessorExpressionSimplifier : CSharpSyntaxRewriter
    {
        private CompositePreprocessorExpressionEvaluator _expressionEvaluator;

        public PreprocessorExpressionSimplifier(CompositePreprocessorExpressionEvaluator expressionEvaluator)
        {
            _expressionEvaluator = expressionEvaluator;
        }

        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            Tristate leftState = _expressionEvaluator.EvaluateExpression(node.Left);
            Tristate rightState = _expressionEvaluator.EvaluateExpression(node.Right);

            var left = (ExpressionSyntax)node.Left.Accept(this);
            var right = (ExpressionSyntax)node.Right.Accept(this);

            if (node.Left != left)
            {
                node = node.WithLeft(left);
            }

            if (node.Right != right)
            {
                node = node.WithRight(right);
            }

            if (leftState != Tristate.Varying && rightState != Tristate.Varying)
            {
                return node;
            }

            ExpressionSyntax newExpression = null;

            if (node.Kind() == SyntaxKind.LogicalAndExpression)
            {
                if (leftState == Tristate.True)
                {
                    // true && varying == varying
                    newExpression = right;
                }
                else if (rightState == Tristate.True)
                {
                    // varying && true == varying
                    newExpression = left;
                }
            }
            else if (node.Kind() == SyntaxKind.LogicalOrExpression)
            {
                if (leftState == Tristate.False)
                {
                    // false || varying == varying
                    newExpression = right;
                }
                else if (rightState == Tristate.False)
                {
                    // varying || false == varying
                    newExpression = left;
                }
            }

            if (newExpression != null)
            {
                return newExpression
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            return node;
        }

        public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            if (node.Kind() != SyntaxKind.LogicalNotExpression)
            {
                throw new InvalidPreprocessorExpressionException("Expected logical not expression");
            }

            var newExpression = (ExpressionSyntax)node.Operand.Accept(this);
            if (newExpression.Kind() == SyntaxKind.LogicalNotExpression)
            {
                return ((PrefixUnaryExpressionSyntax)newExpression).Operand
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
            else
            {
                return node.Operand != newExpression ?
                    node.WithOperand(newExpression) : node;
            }
        }

        public override SyntaxNode VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            var newExpression = (ExpressionSyntax)node.Expression.Accept(this);

            // Remove unnecessary parentheses around non-binary expressions
            if (!(newExpression is BinaryExpressionSyntax))
            {
                return newExpression
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
            else
            {
                return node.Expression != newExpression ?
                    node.WithExpression(newExpression) : node;
            }
        }
    }
}
