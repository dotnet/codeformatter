// Copyright(c) Microsoft.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRule(RemoveRegionsRule.Name, RemoveRegionsRule.Description, SyntaxRuleOrder.RemoveRegionsRule)]
    internal sealed class RemoveRegionsRule : CSharpOnlyFormattingRule, ISyntaxFormattingRule
    {
        internal const string Name = "RemoveRegions";
        internal const string Description = "Removes all regions";

        public SyntaxNode Process(SyntaxNode targetNode, string languageName)
        {
            var finder = new RegionFinder();
            finder.Visit(targetNode);
            var results = finder.Results;

            if (results.Count > 0)
            {
                return targetNode.ReplaceTrivia(results,
                    (arg1, arg2) => new SyntaxTrivia());
            }
            return targetNode;
        }

        private class RegionFinder : CSharpSyntaxWalker
        {
            public List<SyntaxTrivia> Results { get; } = new List<SyntaxTrivia>();

            public RegionFinder()
                : base(SyntaxWalkerDepth.StructuredTrivia)
            {
            }

            public override void VisitRegionDirectiveTrivia(RegionDirectiveTriviaSyntax node)
            {
                Results.Add(node.ParentTrivia);
            }

            public override void VisitEndRegionDirectiveTrivia(EndRegionDirectiveTriviaSyntax node)
            {
                Results.Add(node.ParentTrivia);
            }
        }
    }
}