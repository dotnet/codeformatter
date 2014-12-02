using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeGeneration;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(1)]
    [PartMetadata(RuleTypeConstants.PartMetadataKey, RuleTypeConstants.ConvertTestsRuleType)]
    internal sealed class UsesXunitForTests : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken) as CompilationUnitSyntax;

            if (root == null)
                return document;

            var originalRoot = root;

            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            List<UsingDirectiveSyntax> newUsings = new List<UsingDirectiveSyntax>();
            bool needsChanges = false;

            foreach (var usingSyntax in root.Usings)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(usingSyntax.Name);
                if (symbolInfo.Symbol != null)
                {
                    string namespaceDocID = symbolInfo.Symbol.GetDocumentationCommentId();
                    if (namespaceDocID == "N:Microsoft.VisualStudio.TestPlatform.UnitTestFramework" ||
                        namespaceDocID == "N:Microsoft.Bcl.Testing" ||
                        namespaceDocID == "N:Microsoft.VisualStudio.TestTools.UnitTesting")
                    {
                        needsChanges = true;
                    }
                    else
                    {
                        UsingDirectiveSyntax usingDirectiveToUse = usingSyntax;
                        if (usingDirectiveToUse.HasLeadingTrivia)
                        {
                            var newLeadingTrivia = RemoveCompilerDirectives(usingDirectiveToUse.GetLeadingTrivia());
                            usingDirectiveToUse = usingDirectiveToUse.WithLeadingTrivia(newLeadingTrivia);
                        }
                        if (usingDirectiveToUse.HasTrailingTrivia)
                        {
                            var newTrailingTrivia = RemoveCompilerDirectives(usingDirectiveToUse.GetTrailingTrivia());
                            usingDirectiveToUse = usingDirectiveToUse.WithTrailingTrivia(newTrailingTrivia);
                        }

                        newUsings.Add(usingDirectiveToUse);
                    }
                }
            }

            if (!needsChanges)
            {
                return document;
            }

            TransformationTracker transformationTracker = new TransformationTracker();
            RemoveTestClassAttributes(root, semanticModel, transformationTracker);
            ChangeTestMethodAttributesToFact(root, semanticModel, transformationTracker);
            root = transformationTracker.TransformRoot(root);


            //  Remove compiler directives before the first member of the file (e.g. an #endif after the using statements)
            var firstMember = root.Members.FirstOrDefault();
            if (firstMember != null)
            {
                if (firstMember.HasLeadingTrivia)
                {
                    var newLeadingTrivia = RemoveCompilerDirectives(firstMember.GetLeadingTrivia());
                    root = root.ReplaceNode(firstMember, firstMember.WithLeadingTrivia(newLeadingTrivia));
                }
            }
            
            var xUnitUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Xunit")).NormalizeWhitespace();
            newUsings.Add(xUnitUsing);

            //  Apply trailing trivia from original last using statement to new last using statement
            SyntaxTriviaList usingTrailingTrivia = RemoveCompilerDirectives(originalRoot.Usings.Last().GetTrailingTrivia());
            newUsings[newUsings.Count - 1] = newUsings.Last().WithTrailingTrivia(usingTrailingTrivia);

            root = root.WithUsings(SyntaxFactory.List<UsingDirectiveSyntax>(newUsings));


            return document.WithSyntaxRoot(root);
        }

        private void RemoveTestClassAttributes(CompilationUnitSyntax root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            List<AttributeSyntax> nodesToRemove = new List<AttributeSyntax>();

            foreach (var attributeListSyntax in root.DescendantNodes().OfType<AttributeListSyntax>())
            {
                var attributesToRemove = attributeListSyntax.Attributes.Where(attributeSyntax =>
                {
                    var typeInfo = semanticModel.GetTypeInfo(attributeSyntax);
                    if (typeInfo.Type != null)
                    {
                        string attributeTypeDocID = typeInfo.Type.GetDocumentationCommentId();
                        if (attributeTypeDocID == "T:Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute")
                        {
                            return true;

                        }
                    }
                    return false;
                }).ToList();

                nodesToRemove.AddRange(attributesToRemove);
            }

            transformationTracker.AddTransformation(nodesToRemove, (transformationRoot, rewrittenNodes) =>
            {
                foreach (AttributeSyntax rewrittenNode in rewrittenNodes)
                {
                    var attributeListSyntax = (AttributeListSyntax)rewrittenNode.Parent;
                    var newSyntaxList = attributeListSyntax.Attributes.Remove(rewrittenNode);
                    if (newSyntaxList.Any())
                    {
                        transformationRoot = transformationRoot.ReplaceNode(attributeListSyntax, attributeListSyntax.WithAttributes(newSyntaxList));
                    }
                    else
                    {
                        transformationRoot = transformationRoot.RemoveNode(attributeListSyntax, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                }
                return transformationRoot;
            });

        }
        private void ChangeTestMethodAttributesToFact(CompilationUnitSyntax root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            List<AttributeSyntax> nodesToReplace = new List<AttributeSyntax>();

            foreach (var attributeSyntax in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                var typeInfo = semanticModel.GetTypeInfo(attributeSyntax);
                if (typeInfo.Type != null)
                {
                    string attributeTypeDocID = typeInfo.Type.GetDocumentationCommentId();
                    if (attributeTypeDocID == "T:Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute")
                    {
                        nodesToReplace.Add(attributeSyntax);
                    }
                }
            }

            transformationTracker.AddTransformation(nodesToReplace, (transformationRoot, rewrittenNodes) =>
            {
                return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                {
                    return ((AttributeSyntax)rewrittenNode).WithName(SyntaxFactory.ParseName("Fact")).NormalizeWhitespace();
                });
            });
            
        }

        private static SyntaxTriviaList RemoveCompilerDirectives(SyntaxTriviaList stl)
        {
            foreach (var trivia in stl)
            {
                if (trivia.CSharpKind() == SyntaxKind.IfDirectiveTrivia ||
                    trivia.CSharpKind() == SyntaxKind.DisabledTextTrivia ||
                    trivia.CSharpKind() == SyntaxKind.EndIfDirectiveTrivia ||
                    trivia.CSharpKind() == SyntaxKind.ElifDirectiveTrivia ||
                    trivia.CSharpKind() == SyntaxKind.ElseDirectiveTrivia)
                {
                    stl = stl.Remove(trivia);
                }
            }

            return stl;
        }

        class TransformationTracker
        {
            Dictionary<SyntaxAnnotation, Func<CompilationUnitSyntax, IEnumerable<SyntaxNode>, CompilationUnitSyntax>> _annotationToTransformation = new Dictionary<SyntaxAnnotation, Func<CompilationUnitSyntax, IEnumerable<SyntaxNode>, CompilationUnitSyntax>>();
            Dictionary<SyntaxNode, List<SyntaxAnnotation>> _nodeToAnnotations = new Dictionary<SyntaxNode, List<SyntaxAnnotation>>();

            public void AddTransformation(IEnumerable<SyntaxNode> nodesToTransform, Func<CompilationUnitSyntax, IEnumerable<SyntaxNode>, CompilationUnitSyntax> transformerFunc)
            {
                var annotation = new SyntaxAnnotation();
                _annotationToTransformation[annotation] = transformerFunc;

                foreach (var node in nodesToTransform)
                {
                    List<SyntaxAnnotation> annotationsForNode;
                    if (!_nodeToAnnotations.TryGetValue(node, out annotationsForNode))
                    {
                        annotationsForNode = new List<SyntaxAnnotation>();
                        _nodeToAnnotations[node] = annotationsForNode;
                    }
                    annotationsForNode.Add(annotation);
                }
            }

            public CompilationUnitSyntax TransformRoot(CompilationUnitSyntax root)
            {
                root = root.ReplaceNodes(_nodeToAnnotations.Keys, (originalNode, rewrittenNode) =>
                {
                    return rewrittenNode.WithAdditionalAnnotations(_nodeToAnnotations[originalNode]);
                });

                foreach (var kvp in _annotationToTransformation)
                {
                    var syntaxAnnotation = kvp.Key;
                    var transformation = kvp.Value;
                    var nodesToTransform = root.GetAnnotatedNodes(syntaxAnnotation);
                    root = transformation(root, nodesToTransform);
                }

                return root;
            }
        }
    }
}
