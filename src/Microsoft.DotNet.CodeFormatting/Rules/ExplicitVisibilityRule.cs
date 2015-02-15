// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRuleOrder(LocalSemanticRuleOrder.ExplicitVisibilityRule)]
    internal sealed partial class ExplicitVisibilityRule : ILocalSemanticFormattingRule
    {
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
