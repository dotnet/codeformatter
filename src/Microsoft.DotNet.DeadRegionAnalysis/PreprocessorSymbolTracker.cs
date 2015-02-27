// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    internal class PreprocessorSymbolTracker : CSharpSyntaxVisitor
    {
        private readonly IEnumerable<string> _specifiedSymbols;
        private readonly HashSet<string> _unvisitedSymbols;
        private readonly HashSet<string> _visitedSymbols;

        public IEnumerable<string> SpecifiedSymbols { get { return _specifiedSymbols; } }
        public IEnumerable<string> UnvisitedSymbols { get { return _unvisitedSymbols; } }
        public IEnumerable<string> VisitedSymbols { get { return _visitedSymbols; } }

        public PreprocessorSymbolTracker(IEnumerable<string> specifiedSymbols)
        {
            _specifiedSymbols = specifiedSymbols;
            _unvisitedSymbols = new HashSet<string>(specifiedSymbols);
            _visitedSymbols = new HashSet<string>();
        }

        private void VisitSymbol(string symbol)
        {
            _unvisitedSymbols.Remove(symbol);
            _visitedSymbols.Add(symbol);
        }

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.TrueLiteralExpression:
                    VisitSymbol("true");
                    break;
                case SyntaxKind.FalseLiteralExpression:
                    VisitSymbol("false");
                    break;
                default:
                    throw new InvalidPreprocessorExpressionException("Expected true or false literal expression");
            }
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            VisitSymbol(node.ToString());
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            if (node.Kind() != SyntaxKind.LogicalNotExpression)
            {
                throw new InvalidPreprocessorExpressionException("Expected logical not expression");
            }

            node.Operand.Accept(this);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);

            switch (node.Kind())
            {
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                    break;
                default:
                    throw new InvalidPreprocessorExpressionException("Expected logical and/or expression");
            }
        }

        public override void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            node.Expression.Accept(this);
        }
    }
}
