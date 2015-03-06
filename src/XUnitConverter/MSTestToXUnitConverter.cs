// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeGeneration;
using System.Runtime.Serialization;
using System.IO;

namespace XUnitConverter
{
    public sealed class MSTestToXUnitConverter : ConverterBase
    {
        private static object _lockObject = new object();
        private static HashSet<string> _mstestNamespaces;

        private static UsingDirectiveSyntax RemoveLeadingAndTrailingCompilerDirectives(UsingDirectiveSyntax usingSyntax)
        {
            UsingDirectiveSyntax usingDirectiveToUse = usingSyntax;
            if (usingDirectiveToUse.HasLeadingTrivia)
            {
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
            }

            return usingDirectiveToUse;
        }

        protected override async Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var root = syntaxNode as CompilationUnitSyntax;
            if (root == null)
                return document.Project.Solution;

            if (!LoadMSTestNamespaces())
            {
                return document.Project.Solution;
            }

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
                    if (_mstestNamespaces.Contains(namespaceDocID))
                    {
                        needsChanges = true;
                    }
                    else
                    {
                        newUsings.Add(RemoveLeadingAndTrailingCompilerDirectives(usingSyntax));
                    }
                }
                else
                {
                    newUsings.Add(RemoveLeadingAndTrailingCompilerDirectives(usingSyntax));
                }
            }

            if (!needsChanges)
            {
                return document.Project.Solution;
            }

            TransformationTracker transformationTracker = new TransformationTracker();
            RemoveTestClassAttributes(root, semanticModel, transformationTracker);
            RemoveContractsRequiredAttributes(root, semanticModel, transformationTracker);
            ChangeTestMethodAttributesToFact(root, semanticModel, transformationTracker);
            ChangeAssertCalls(root, semanticModel, transformationTracker);
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


            return document.WithSyntaxRoot(root).Project.Solution;
        }

        private void RemoveContractsRequiredAttributes(CompilationUnitSyntax root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            RemoveTestAttributes(root, semanticModel, transformationTracker, "ContractsRequiredAttribute");
        }

        private void RemoveTestClassAttributes(CompilationUnitSyntax root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            RemoveTestAttributes(root, semanticModel, transformationTracker, "TestClassAttribute");
        }

        private void RemoveTestAttributes(CompilationUnitSyntax root, SemanticModel semanticModel, TransformationTracker transformationTracker, string attributeName)
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
                        if (IsTestNamespaceType(attributeTypeDocID, attributeName))
                        {
                            return true;
                        }
                    }
                    return false;
                }).ToList();

                nodesToRemove.AddRange(attributesToRemove);
            }

            transformationTracker.AddTransformation(nodesToRemove, (transformationRoot, rewrittenNodes, originalNodeMap) =>
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
                    if (IsTestNamespaceType(attributeTypeDocID, "TestMethodAttribute"))
                    {
                        nodesToReplace.Add(attributeSyntax);
                    }
                }
            }

            transformationTracker.AddTransformation(nodesToReplace, (transformationRoot, rewrittenNodes, originalNodeMap) =>
            {
                return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                {
                    return ((AttributeSyntax)rewrittenNode).WithName(SyntaxFactory.ParseName("Fact")).NormalizeWhitespace();
                });
            });
        }

        private void ChangeAssertCalls(CompilationUnitSyntax root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            Dictionary<string, string> assertMethodsToRename = new Dictionary<string, string>()
            {
                { "AreEqual", "Equal" },
                { "AreNotEqual", "NotEqual" },
                { "IsNull", "Null" },
                { "IsNotNull", "NotNull" },
                { "AreSame", "Same" },
                { "AreNotSame", "NotSame" },
                { "IsTrue", "True" },
                { "IsFalse", "False" },
                { "IsInstanceOfType", "IsAssignableFrom" },
            };

            Dictionary<SimpleNameSyntax, string> nameReplacementsForNodes = new Dictionary<SimpleNameSyntax, string>();
            List<InvocationExpressionSyntax> methodCallsToReverseArguments = new List<InvocationExpressionSyntax>();

            foreach (var methodCallSyntax in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var expressionSyntax = methodCallSyntax.Expression;
                var expressionTypeInfo = semanticModel.GetTypeInfo(expressionSyntax);
                if (expressionTypeInfo.Type != null)
                {
                    string expressionDocID = expressionTypeInfo.Type.GetDocumentationCommentId();
                    if (IsTestNamespaceType(expressionDocID, "Assert"))
                    {
                        string newMethodName;
                        if (assertMethodsToRename.TryGetValue(methodCallSyntax.Name.Identifier.Text, out newMethodName))
                        {
                            nameReplacementsForNodes.Add(methodCallSyntax.Name, newMethodName);

                            if (newMethodName == "IsAssignableFrom" && methodCallSyntax.Parent is InvocationExpressionSyntax)
                            {
                                //  Parameter order is reversed between MSTest Assert.IsInstanceOfType and xUnit Assert.IsAssignableFrom
                                methodCallsToReverseArguments.Add((InvocationExpressionSyntax)methodCallSyntax.Parent);
                            }
                        }
                    }
                }
            }

            if (nameReplacementsForNodes.Any())
            {
                transformationTracker.AddTransformation(nameReplacementsForNodes.Keys, (transformationRoot, rewrittenNodes, originalNodeMap) =>
                {
                    return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                    {
                        var realOriginalNode = (SimpleNameSyntax)originalNodeMap[originalNode];
                        string newName = nameReplacementsForNodes[realOriginalNode];
                        return SyntaxFactory.ParseName(newName);
                    });
                });

                transformationTracker.AddTransformation(methodCallsToReverseArguments, (transformationRoot, rewrittenNodes, originalNodeMap) =>
                {
                    return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                    {
                        var invocationExpression = (InvocationExpressionSyntax)rewrittenNode;
                        var oldArguments = invocationExpression.ArgumentList.Arguments;
                        var newArguments = new SeparatedSyntaxList<ArgumentSyntax>().AddRange(new[] { oldArguments[1], oldArguments[0] });

                        return invocationExpression.WithArgumentList(invocationExpression.ArgumentList.WithArguments(newArguments));
                    });
                });
            }
        }

        private static SyntaxTriviaList RemoveCompilerDirectives(SyntaxTriviaList stl)
        {
            foreach (var trivia in stl)
            {
                if (trivia.Kind() == SyntaxKind.IfDirectiveTrivia ||
                    trivia.Kind() == SyntaxKind.DisabledTextTrivia ||
                    trivia.Kind() == SyntaxKind.EndIfDirectiveTrivia ||
                    trivia.Kind() == SyntaxKind.ElifDirectiveTrivia ||
                    trivia.Kind() == SyntaxKind.ElseDirectiveTrivia)
                {
                    stl = stl.Remove(trivia);
                }
            }

            return stl;
        }

        private static bool IsTestNamespaceType(string docID, string simpleTypeName)
        {
            if (docID == null)
            {
                return false;
            }

            int lastPeriod = docID.LastIndexOf('.');
            if (lastPeriod < 0)
            {
                return false;
            }

            string simpleTypeNameFromDocID = docID.Substring(lastPeriod + 1);
            if (simpleTypeNameFromDocID != simpleTypeName)
            {
                return false;
            }

            string namespaceDocID = "N" + docID.Substring(1, lastPeriod - 1);
            return _mstestNamespaces.Contains(namespaceDocID);
        }

        private bool LoadMSTestNamespaces()
        {
            lock (_lockObject)
            {
                if (_mstestNamespaces != null)
                {
                    return true;
                }

                var filePath = Path.Combine(
                    Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(typeof(MSTestToXUnitConverter).Assembly.CodeBase).Path)),
                    "MSTestNamespaces.txt");

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("The MSTestNamespaces.txt file was not found.");
                    return false;
                }

                var lines = File.ReadAllLines(filePath);
                _mstestNamespaces = new HashSet<string>(lines);
                return true;
            }
        }

        private class TransformationTracker
        {
            private Dictionary<SyntaxAnnotation, Func<CompilationUnitSyntax, IEnumerable<SyntaxNode>, Dictionary<SyntaxNode, SyntaxNode>, CompilationUnitSyntax>> _annotationToTransformation = new Dictionary<SyntaxAnnotation, Func<CompilationUnitSyntax, IEnumerable<SyntaxNode>, Dictionary<SyntaxNode, SyntaxNode>, CompilationUnitSyntax>>();
            private Dictionary<SyntaxNode, List<SyntaxAnnotation>> _nodeToAnnotations = new Dictionary<SyntaxNode, List<SyntaxAnnotation>>();
            private Dictionary<SyntaxAnnotation, SyntaxNode> _originalNodeLookup = new Dictionary<SyntaxAnnotation, SyntaxNode>();

            public void AddTransformation(IEnumerable<SyntaxNode> nodesToTransform, Func<CompilationUnitSyntax, IEnumerable<SyntaxNode>, Dictionary<SyntaxNode, SyntaxNode>, CompilationUnitSyntax> transformerFunc)
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

                    var originalNodeAnnotation = new SyntaxAnnotation();
                    _originalNodeLookup[originalNodeAnnotation] = node;
                    annotationsForNode.Add(originalNodeAnnotation);
                }
            }

            public CompilationUnitSyntax TransformRoot(CompilationUnitSyntax root)
            {
                root = root.ReplaceNodes(_nodeToAnnotations.Keys, (originalNode, rewrittenNode) =>
                {
                    var ret = rewrittenNode.WithAdditionalAnnotations(_nodeToAnnotations[originalNode]);

                    return ret;
                });

                foreach (var kvp in _annotationToTransformation)
                {
                    Dictionary<SyntaxNode, SyntaxNode> originalNodeMap = new Dictionary<SyntaxNode, SyntaxNode>();
                    foreach (var originalNodeKvp in _originalNodeLookup)
                    {
                        var annotatedNodes = root.GetAnnotatedNodes(originalNodeKvp.Key).ToList();
                        SyntaxNode annotatedNode = annotatedNodes.SingleOrDefault();
                        if (annotatedNode != null)
                        {
                            originalNodeMap[annotatedNode] = originalNodeKvp.Value;
                        }
                    }

                    var syntaxAnnotation = kvp.Key;
                    var transformation = kvp.Value;
                    var nodesToTransform = root.GetAnnotatedNodes(syntaxAnnotation);
                    root = transformation(root, nodesToTransform, originalNodeMap);
                }

                return root;
            }
        }
    }
}
