using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(RuleOrder.RemoveExplicitThisRule)]
    public sealed class ExplicitThisRule : IFormattingRule
    {
        private sealed class ExplicitThisRewriter : CSharpSyntaxRewriter
        {
            private readonly Document _document;
            private readonly CancellationToken _cancellationToken;
            private SemanticModel _semanticModel;
            private bool _addedAnnotations;

            internal bool AddedAnnotations
            {
                get { return _addedAnnotations; }
            }

            internal ExplicitThisRewriter(Document document, CancellationToken cancellationToken)
            {
                _document = document;
                _cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                node = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node);
                var name = node.Name.Identifier.ValueText;
                if (node.Expression != null &&
                    node.Expression.CSharpKind() == SyntaxKind.ThisExpression &&
                    name.StartsWith("_", StringComparison.Ordinal) &&
                    IsPrivateField(node))
                {
                    _addedAnnotations = true;
                    return node.WithAdditionalAnnotations(Simplifier.Annotation);
                }

                return node;
            }

            private bool IsPrivateField(MemberAccessExpressionSyntax memberSyntax)
            {
                if (_semanticModel == null)
                {
                    _semanticModel = _document.GetSemanticModelAsync(_cancellationToken).Result;
                }

                var symbolInfo = _semanticModel.GetSymbolInfo(memberSyntax, _cancellationToken);
                if (symbolInfo.Symbol != null && symbolInfo.Symbol.Kind == SymbolKind.Field)
                {
                    var field = (IFieldSymbol)symbolInfo.Symbol;
                    return field.DeclaredAccessibility == Accessibility.Private;
                }

                return false;
            }
        }

        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
            {
                return document;
            }

            var rewriter = new ExplicitThisRewriter(document, cancellationToken);
            var newNode = rewriter.Visit(syntaxNode);
            if (!rewriter.AddedAnnotations)
            {
                return document;
            }

            return await Simplifier.ReduceAsync(document.WithSyntaxRoot(newNode), cancellationToken: cancellationToken);
        }
    }
}