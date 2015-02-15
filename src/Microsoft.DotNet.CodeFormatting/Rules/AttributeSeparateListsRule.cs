// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRuleOrder(SyntaxRuleOrder.AttributeSeparateListsRule)]
    internal sealed class AttributeSeparateListsRule : ISyntaxFormattingRule
    {
        public SyntaxNode Process(SyntaxNode syntaxRoot)
        {
            var rewriter = new AttributeListRewriter();
            return rewriter.Visit(syntaxRoot);
        }

        private sealed class AttributeListRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitParameter(ParameterSyntax node)
            {
                // We don't want to flatten the attribute lists for parameters. Those are
                // usually short, such as [In, Out] and collapsing them can actually
                // improve readability.
                return node;
            }

            public override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list)
            {
                list = base.VisitList(list);

                if (typeof (TNode) != typeof (AttributeListSyntax))
                    return list;

                var attributeLists = (SyntaxList<AttributeListSyntax>) (object) list;
                return (SyntaxList<TNode>) (object) VisitAttributeLists(attributeLists);
            }

            private static SyntaxList<AttributeListSyntax> VisitAttributeLists(SyntaxList<AttributeListSyntax> attributeLists)
            {
                var result = new List<AttributeListSyntax>();

                foreach (var attributeList in attributeLists)
                {
                    var firstIndex = result.Count;

                    for (var i = 0; i < attributeList.Attributes.Count; i++)
                    {
                        var attribute = attributeList.Attributes[i];
                        var separatorTrivia = i < attributeList.Attributes.Count - 1
                                                ? attributeList.Attributes.GetSeparator(i).GetAllTrivia()
                                                : Enumerable.Empty<SyntaxTrivia>();

                        var attributeWithoutTrivia = attribute.WithoutLeadingTrivia().WithoutTrailingTrivia();
                        var singletonList = SyntaxFactory.AttributeList(attributeList.Target, SyntaxFactory.SeparatedList(new[] { attributeWithoutTrivia }))
                                                         .WithLeadingTrivia(attribute.GetLeadingTrivia())
                                                         .WithTrailingTrivia(attribute.GetTrailingTrivia().Concat(separatorTrivia));
                        result.Add(singletonList);
                    }

                    var lastIndex = result.Count - 1;

                    var leadingTrivia = attributeList.GetLeadingTrivia()
                                            .Concat(attributeList.OpenBracketToken.TrailingTrivia)
                                            .Concat(result[firstIndex].GetLeadingTrivia());

                    var trailingTrivia = result[lastIndex].GetTrailingTrivia()
                                            .Concat(attributeList.CloseBracketToken.LeadingTrivia)
                                            .Concat(attributeList.GetTrailingTrivia());

                    result[firstIndex] = result[firstIndex].WithLeadingTrivia(leadingTrivia);
                    result[lastIndex] = result[lastIndex].WithTrailingTrivia(trailingTrivia);
                }

                return SyntaxFactory.List(result);
            }
        }
    }
}