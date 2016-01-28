// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRule(NonAsciiCharactersAreEscapedInLiterals.Name, NonAsciiCharactersAreEscapedInLiterals.Description, SyntaxRuleOrder.NonAsciiChractersAreEscapedInLiterals)]
    internal sealed class NonAsciiCharactersAreEscapedInLiterals : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        internal const string Name = FormattingDefaults.UnicodeLiteralsRuleName;
        internal const string Description = "Use unicode escape sequence instead of unicode literals";

        public SyntaxNode Process(SyntaxNode root, string languageName)
        {
            return UnicodeCharacterEscapingSyntaxRewriter.Rewriter.Visit(root);
        }

        /// <summary>
        ///  Rewrites string and character literals which contain non ascii characters to instead use the \uXXXX or \UXXXXXXXX syntax.
        /// </summary>
        internal class UnicodeCharacterEscapingSyntaxRewriter : CSharpSyntaxRewriter
        {
            public static readonly UnicodeCharacterEscapingSyntaxRewriter Rewriter = new UnicodeCharacterEscapingSyntaxRewriter();

            private UnicodeCharacterEscapingSyntaxRewriter()
            {
            }

            public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node)
            {
                switch (node.Kind())
                {
                    case SyntaxKind.StringLiteralExpression:
                        return RewriteStringLiteralExpression(node);
                    case SyntaxKind.CharacterLiteralExpression:
                        return RewriteCharacterLiteralExpression(node);
                }

                return node;
            }

            private static SyntaxNode RewriteStringLiteralExpression(LiteralExpressionSyntax node)
            {
                Debug.Assert(node.Kind() == SyntaxKind.StringLiteralExpression);

                if (node.Token.IsVerbatimStringLiteral())
                {
                    // We do not correctly rewrite verbatim string literals yet.  Once Issue 39 is
                    // fixed we can remove this early out.
                    return node;
                }

                if (HasNonAsciiCharacters(node.Token.Text))
                {
                    string convertedText = EscapeNonAsciiCharacters(node.Token.Text);

                    SyntaxToken t = SyntaxFactory.Literal(node.Token.LeadingTrivia, convertedText, node.Token.ValueText, node.Token.TrailingTrivia);

                    node = node.WithToken(t);
                }

                return node;
            }

            private static SyntaxNode RewriteCharacterLiteralExpression(LiteralExpressionSyntax node)
            {
                Debug.Assert(node.Kind() == SyntaxKind.CharacterLiteralExpression);

                if (HasNonAsciiCharacters(node.Token.Text))
                {
                    string convertedText = EscapeNonAsciiCharacters(node.Token.Text);

                    SyntaxToken t = SyntaxFactory.Literal(node.Token.LeadingTrivia, convertedText, node.Token.ValueText, node.Token.TrailingTrivia);

                    node = node.WithToken(t);
                }

                return node;
            }


            private static bool HasNonAsciiCharacters(string value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] >= 0x80)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string EscapeNonAsciiCharacters(string oldValue)
            {
                StringBuilder sb = new StringBuilder(oldValue.Length);

                for (int i = 0; i < oldValue.Length; i++)
                {
                    if (oldValue[i] < 0x80)
                    {
                        sb.Append(oldValue[i]);
                    }
                    else if (char.IsHighSurrogate(oldValue[i]) && i + 1 < oldValue.Length && char.IsLowSurrogate(oldValue[i + 1]))
                    {
                        sb.Append(string.Format(@"\U{0:X8}", char.ConvertToUtf32(oldValue[i], oldValue[i + 1])));
                        i++; // move past the low surogate we consumed above.
                    }
                    else
                    {
                        sb.Append(string.Format(@"\u{0:X4}", (ushort)oldValue[i]));
                    }
                }

                return sb.ToString();
            }
        }
    }
}
