// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRule(Name = ExplicitVisibilityRule.Name, Description = ExplicitVisibilityRule.Description, Order = LocalSemanticRuleOrder.ExplicitVisibilityRule)]
    internal sealed partial class ExplicitVisibilityRule : ILocalSemanticFormattingRule
    {
        internal const string Name = "ExplicitVisibility";
        internal const string Description = "Ensure all members have an explicit visibility modifier";

        public bool SupportsLanguage(string languageName)
        {
            return
                languageName == LanguageNames.CSharp ||
                languageName == LanguageNames.VisualBasic;
        }

        public Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken)
        {
            SyntaxNode result;
            switch (document.Project.Language)
            {
                case LanguageNames.CSharp:
                    {
                        var rewriter = new CSharpVisibilityRewriter(document, cancellationToken);
                        result = rewriter.Visit(syntaxRoot);
                        break;
                    }
                case LanguageNames.VisualBasic:
                    {
                        var rewriter = new VisualBasicVisibilityRewriter(document, cancellationToken);
                        result = rewriter.Visit(syntaxRoot);
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }

            return Task.FromResult(result);
        }
    }
}
