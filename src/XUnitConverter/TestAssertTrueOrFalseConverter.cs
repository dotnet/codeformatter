// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XUnitConverter
{
    public sealed class TestAssertTrueOrFalseConverter  : ConverterBase
    {
        private readonly AssertTrueOrFalseRewriter _rewriter = new AssertTrueOrFalseRewriter();

        protected override Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var newNode = _rewriter.Visit(syntaxNode);
            if (newNode != syntaxNode)
            {
                document = document.WithSyntaxRoot(newNode);
            }

            return Task.FromResult(document.Project.Solution);
        }

        internal sealed class AssertTrueOrFalseRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax syntaxNode)
            {
                bool? isAssertTF = IsAssertTrueOrFalse(syntaxNode);
                if (isAssertTF != null)
                {
                    string firstArg = null, secondArg = null;

                    var expr = syntaxNode.Expression as InvocationExpressionSyntax;
                    var firstArgNode = expr.ArgumentList.Arguments.First().Expression;

                    if (firstArgNode.IsKind(SyntaxKind.LogicalNotExpression))
                    {
                        // revert True and False
                        string fmt = isAssertTF.Value ? AssertFalseNoMsg : AssertTrueNoMsg;
                        // the first char should be !
                        // the comments associated with this arg will be lost
                        firstArg = firstArgNode.ToString().Trim().Substring(1);
                        if (expr.ArgumentList.Arguments.Count == 2)
                        {
                            secondArg = expr.ArgumentList.Arguments.Last().ToString().Trim();
                            fmt = isAssertTF.Value ? AssertFalse : AssertTrue;
                        }

                        return SyntaxFactory.ParseStatement(syntaxNode.GetLeadingTrivia().ToFullString() +
                            string.Format(fmt, firstArg, secondArg) +
                            syntaxNode.GetTrailingTrivia().ToFullString());
                    }
                    else if (firstArgNode.IsKind(SyntaxKind.EqualsExpression) || firstArgNode.IsKind(SyntaxKind.NotEqualsExpression))
                    {
                        BinaryExpressionSyntax expr2 = firstArgNode as BinaryExpressionSyntax;
                        firstArg = expr2.Left.ToString().Trim();
                        secondArg = expr2.Right.ToString().Trim();

                        bool isEqual = firstArgNode.IsKind(SyntaxKind.EqualsExpression);
                        // Assert.True(a==b) || Assert.False(a!=b)
                        bool positive = isAssertTF.Value && isEqual || !(isAssertTF.Value || isEqual);
                        var fmt = positive ? AssertEqual : AssertNotEqual;

                        // special case
                        if (IsSpecialValue(ref firstArg, ref secondArg, "null"))
                        {
                            // Assert.True(cond ==|!= null) || Assert.False(cond ==|!= null)
                            fmt = positive ? AssertNull : AssertNotNull;
                        }
                        else if (IsSpecialValue(ref firstArg, ref secondArg, "true"))
                        {
                            // Assert.True(cond ==|!= true) || Assert.False(cond ==|!= true)
                            fmt = positive ? AssertTrueNoMsg : AssertFalseNoMsg;
                        }
                        else if (IsSpecialValue(ref firstArg, ref secondArg, "false"))
                        {
                            // Assert.True(cond ==|!= false) || Assert.False(cond ==|!= false)
                            fmt = positive ? AssertFalseNoMsg : AssertTrueNoMsg;
                        }
                        else
                        {
                            int v = 0;
                            // if second is a const (int only for now)
                            if (int.TryParse(secondArg, out v))
                            {
                                // swap
                                string tmp = firstArg;
                                firstArg = secondArg;
                                secondArg = tmp;
                            }
                        }

                        return SyntaxFactory.ParseStatement(
                                syntaxNode.GetLeadingTrivia().ToFullString() +
                                string.Format(fmt, firstArg, secondArg) +
                                syntaxNode.GetTrailingTrivia().ToFullString());
                    }
                }

                return base.VisitExpressionStatement(syntaxNode);
            }

            #region "Helper"

            public const string AssertTrue = "Assert.True({0}, {1});";
            public const string AssertFalse = "Assert.False({0}, {1});";
            public const string AssertTrueNoMsg = "Assert.True({0});";
            public const string AssertFalseNoMsg = "Assert.False({0});";
            public const string AssertEqual = "Assert.Equal({0}, {1});";
            public const string AssertNotEqual = "Assert.NotEqual({0}, {1});";
            public const string AssertNull = "Assert.Null({0});";
            public const string AssertNotNull = "Assert.NotNull({0});";

            public static bool IsSpecialValue(ref string first, ref string second, string cond)
            {
                if (first == cond || second == cond)
                {
                    if (first == cond)
                        first = second;

                    second = null;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="node"></param>
            /// <returns>true=Assert.True; false=Assert.False; null=Neither </returns>
            public static bool? IsAssertTrueOrFalse(SyntaxNode node)
            {
                if (node != null && node.IsKind(SyntaxKind.ExpressionStatement))
                {
                    var invoke = (node as ExpressionStatementSyntax).Expression as InvocationExpressionSyntax;
                    if (invoke != null)
                    {
                        var expr = invoke.Expression as MemberAccessExpressionSyntax;
                        if (!(expr == null || expr.Name == null || expr.Expression == null))
                        {
                            var id = expr.Name.Identifier.ToString().Trim();
                            var caller = expr.Expression.ToString().Trim();

                            if (caller == "Assert")
                            {
                                if (id == "True")
                                    return true;
                                if (id == "False")
                                    return false;
                            }
                        }
                    }
                }

                return null;
            }

            #endregion
        }
    }
}
