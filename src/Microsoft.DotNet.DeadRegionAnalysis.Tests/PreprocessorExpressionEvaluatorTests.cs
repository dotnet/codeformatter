using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;
using System;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class PreprocessorExpressionEvaluatorTests
    {
        [Fact]
        public void TrueLiteral()
        {
            Verify("true", Tristate.True);
        }

        [Fact]
        public void FalseLiteral()
        {
            Verify("false", Tristate.False);
        }

        [Fact]
        public void LogicalAnd()
        {
            Verify("true && true", Tristate.True);
            Verify("true && false", Tristate.False);
            Verify("true && varying", Tristate.Varying);
            Verify("false && false", Tristate.False);
            Verify("false && varying", Tristate.False);
        }

        [Fact]
        public void LogicalOr()
        {
            Verify("true || true", Tristate.True);
            Verify("true || false", Tristate.True);
            Verify("true || varying", Tristate.True);
            Verify("false || false", Tristate.False);
            Verify("false || varying", Tristate.Varying);
        }

        [Fact]
        public void LogicalNot()
        {
            Verify("!true", Tristate.False);
            Verify("!false", Tristate.True);
            Verify("!varying", Tristate.Varying);
        }

        [Fact]
        public void ParentheicalExpressions()
        {
            Verify("!(true && (false || true))", Tristate.False);
            Verify("(!((true)))", Tristate.False);
            Verify("(!(!true))", Tristate.True);
        }

        [Fact]
        public void InvalidExpressions()
        {
            VerifyInvalid("false + true");
            VerifyInvalid("&varying");
            VerifyInvalid("1");
        }

        private static readonly CSharpParseOptions s_expressionParseOptions = new CSharpParseOptions(
                    documentationMode: DocumentationMode.None,
                    kind: SourceCodeKind.Interactive);

        private static readonly PreprocessorExpressionEvaluator s_evaluator = new PreprocessorExpressionEvaluator(
            new Dictionary<string, Tristate>() { { "varying", Tristate.Varying } });

        private static Tristate EvaluateExpression(string expression)
        {
            var expressionSyntax = SyntaxFactory.ParseExpression(
                expression,
                options: s_expressionParseOptions);

            return expressionSyntax.Accept(s_evaluator);
        }

        private static void Verify(string expression, Tristate expectedValue)
        {
            Assert.Equal(expectedValue, EvaluateExpression(expression));
        }

        private static void VerifyInvalid(string expression)
        {
            Assert.Throws<InvalidPreprocessorExpressionException>(() => EvaluateExpression(expression));
        }
    }
}
