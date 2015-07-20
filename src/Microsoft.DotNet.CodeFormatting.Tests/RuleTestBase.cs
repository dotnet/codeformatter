// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class RuleTestBase : CodeFormattingTestBase
    {
        protected override async Task<Solution> Format(Solution solution, bool runFormatter)
        {
            var documentIds = solution.Projects.SelectMany(p => p.DocumentIds);

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);
                document = await RewriteDocumentAsync(document).ConfigureAwait(false);
                if (runFormatter)
                {
                    document = await Formatter.FormatAsync(document).ConfigureAwait(false);
                }

                solution = document.Project.Solution;
            }

            return solution;
        }

        protected abstract Task<Document> RewriteDocumentAsync(Document document);
    }

    public abstract class SyntaxRuleTestBase : RuleTestBase
    {
        internal abstract ISyntaxFormattingRule Rule
        {
            get;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync();
            syntaxRoot = Rule.Process(syntaxRoot, document.Project.Language);
            return document.WithSyntaxRoot(syntaxRoot);
        }
    }

    public abstract class LocalSemanticRuleTestBase : RuleTestBase
    {
        internal abstract ILocalSemanticFormattingRule Rule
        {
            get;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync();
            syntaxRoot = await Rule.ProcessAsync(document, syntaxRoot, CancellationToken.None);
            return document.WithSyntaxRoot(syntaxRoot);
        }
    }

    public abstract class GlobalSemanticRuleTestBase : RuleTestBase
    {
        internal abstract IGlobalSemanticFormattingRule Rule
        {
            get;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var solution = await Rule.ProcessAsync(document, await document.GetSyntaxRootAsync(), CancellationToken.None);
            return solution.GetDocument(document.Id);
        }
    }
}
