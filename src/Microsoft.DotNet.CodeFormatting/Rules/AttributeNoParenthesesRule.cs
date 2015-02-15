// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRuleOrder(SyntaxRuleOrder.AttributeNoParenthesesRule)]
    internal sealed class AttributeNoParenthesesRule : ISyntaxFormattingRule
    {
        public SyntaxNode Process(SyntaxNode syntaxRoot)
        {
            var attributes = syntaxRoot.DescendantNodes()
                                       .OfType<AttributeSyntax>()
                                       .Where(a => a.ArgumentList != null &&
                                                   a.ArgumentList.Arguments.Count == 0 &&
                                                   (!a.ArgumentList.OpenParenToken.IsMissing || !a.ArgumentList.CloseParenToken.IsMissing));

            return syntaxRoot.ReplaceNodes(attributes, (a, n) => a.WithArgumentList(null));
        }
    }
}