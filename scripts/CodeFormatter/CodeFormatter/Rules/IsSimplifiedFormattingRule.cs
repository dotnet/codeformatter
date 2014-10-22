using System;
using System.Threading;
using System.Threading.Tasks;

using CodeFormatter.Engine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Simplification;

namespace CodeFormatter.Rules
{
    [ExportFormattingRule(Int32.MaxValue / 2)]
    internal sealed class IsSimplifiedFormattingRule : IFormattingRule
    {
        public async Task<Document> ProcessAsync(CancellationToken cancellationToken, Document document)
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

            return await Simplifier.ReduceAsync(annotatedDocument, cancellationToken: cancellationToken);
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