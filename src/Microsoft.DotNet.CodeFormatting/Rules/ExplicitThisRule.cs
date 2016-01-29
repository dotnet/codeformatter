// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    [LocalSemanticRule(ExplicitThisRule.Name, ExplicitThisRule.Description, LocalSemanticRuleOrder.RemoveExplicitThisRule)]
    internal sealed class ExplicitThisRule : CSharpOnlyFormattingRule, ILocalSemanticFormattingRule
    {
        internal const string Name = "ExplicitThis";
        internal const string Description = "Remove explicit this/Me prefixes on expressions except where necessary";

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
                    node.Expression.Kind() == SyntaxKind.ThisExpression &&
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

        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var rewriter = new ExplicitThisRewriter(document, cancellationToken);
            var newNode = rewriter.Visit(syntaxNode);
            if (!rewriter.AddedAnnotations)
            {
                return syntaxNode;
            }

            document = await Simplifier.ReduceAsync(document.WithSyntaxRoot(newNode), cancellationToken: cancellationToken);
            return await document.GetSyntaxRootAsync(cancellationToken);
        }
    }
}
