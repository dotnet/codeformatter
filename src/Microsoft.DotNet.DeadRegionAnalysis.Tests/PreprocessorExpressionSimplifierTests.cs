// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class PreprocessorExpressionSimplifierTests
    {
        [Fact]
        public void SimplifyLogicalAndExpressions()
        {
            Verify("true && varying", "varying");
            Verify("varying && true", "varying");
        }

        [Fact]
        public void DoNotSimplifyLogicalAndExpressions()
        {
            Verify("false && varying", "false && varying");
            Verify("varying && false", "varying && false");
        }

        [Fact]
        public void SimplifyLogicalOrExpressions()
        {
            Verify("false || varying", "varying");
            Verify("varying || false", "varying");
        }

        [Fact]
        public void DoNotSimplifyLogicalOrExpressions()
        {
            Verify("true || varying", "true || varying");
            Verify("varying || true", "varying || true");
        }

        [Fact]
        public void SimplifyLogicalNotExpressions()
        {
            Verify("!!true", "true");
            Verify("!!false", "false");
        }

        [Fact]
        public void RemoveUnnecessaryParentheses()
        {
            Verify("!(!true)", "true");
            Verify("true && (false)", "true && false");
            Verify("(varying) || (true)", "varying || true");
            Verify("((varying) || false)", "varying");
            Verify("((varying || true) || false)", "((varying || true) || false)");
            Verify("(((true) && varying))", "varying");
        }

        [Fact]
        public void ComplexExpression()
        {
            Verify("(true && !!(varying || false) && !(!true))", "varying");
        }

        private static readonly PreprocessorExpressionEvaluator s_evaluator = new PreprocessorExpressionEvaluator(
            new Dictionary<string, Tristate>() { { "varying", Tristate.Varying } }, Tristate.False);

        private static readonly PreprocessorExpressionSimplifier s_simplifier = new PreprocessorExpressionSimplifier(
            new CompositePreprocessorExpressionEvaluator(new[] { s_evaluator }));

        private static readonly CSharpParseOptions s_expressionParseOptions = new CSharpParseOptions(
            documentationMode: DocumentationMode.None,
            kind: SourceCodeKind.Interactive);

        private static void Verify(string expression, string expected)
        {
            var simplifiedExpression = SimplifyExpression(expression);
            Assert.Equal(expected, simplifiedExpression.ToFullString());
        }

        private static void VerifyInvalid(string expression)
        {
            Assert.Throws<InvalidPreprocessorExpressionException>(() => SimplifyExpression(expression));
        }

        private static ExpressionSyntax SimplifyExpression(string expression)
        {
            var expressionSyntax = SyntaxFactory.ParseExpression(
                expression,
                options: s_expressionParseOptions);

            return (ExpressionSyntax)expressionSyntax.Accept(s_simplifier);
        }
    }
}
