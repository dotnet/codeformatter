// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    // [Export(typeof(IFormattingRule))]
    // 
    // This rule is disabled as it causes a lot of noise. It's probably better to rely
    // on folks simplifying the code when using the IDE.
    // 
    // I left it in because in case we want to have batch support for it in the future.
    // 
    internal sealed class IsSimplifiedFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);

            // The simplifier will only simplify nodes that are annotated with a
            // specific annotation. Right now, there is no API on the simplifier
            // to say "ignore all annotations and reduce everything". The
            // workaround is to simply annotate each node. Yep, that's not going
            // to make the GC happy but it will totally get the job done.
            var annotater = new Annotater();
            var annoatedRoot = annotater.Visit(root);
            var annotatedDocument = document.WithSyntaxRoot(annoatedRoot);
            var simplifiedDocument = await Simplifier.ReduceAsync(annotatedDocument, cancellationToken: cancellationToken);

            // Since we had to rewrite the document to include the annotations
            // the simplifiedDocument will always be different from the original
            // document. However, in order to allow later phases to detect changes
            // we only return the simplified document if it's different from the
            // annotated document.
            return simplifiedDocument == annotatedDocument
                    ? document
                    : simplifiedDocument;
        }

        private sealed class Annotater : CSharpSyntaxRewriter
        {
            public override SyntaxNode Visit(SyntaxNode node)
            {
                if (node == null)
                    return null;

                var rewrittenNode = base.Visit(node);
                return rewrittenNode.WithAdditionalAnnotations(Simplifier.Annotation);
            }
        }
    }
}