// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public partial class AnalysisEngine
    {
        private Options _options;
        private CompositePreprocessorExpressionEvaluator _expressionEvaluator;
        private PreprocessorExpressionSimplifier _expressionSimplifier;
        private PreprocessorSymbolTracker _symbolTracker;

        public event Func<DocumentConditionalRegionInfo, CancellationToken, Task> DocumentAnalyzed;

        public static AnalysisEngine FromFilePaths(
            IEnumerable<string> filePaths,
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            Tristate undefinedSymbolValue = default(Tristate))
        {
            if (filePaths == null || !filePaths.Any())
            {
                throw new ArgumentException("Must specify at least one file path");
            }

            IEnumerable<string> projectPaths = null;
            IEnumerable<string> sourcePaths = null;

            var firstFileExt = Path.GetExtension(filePaths.First());
            if (firstFileExt.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                projectPaths = filePaths;
            }
            else
            {
                sourcePaths = filePaths;
            }

            var options = new Options(
                projectPaths: projectPaths,
                sourcePaths: sourcePaths,
                symbolConfigurations: symbolConfigurations,
                alwaysIgnoredSymbols: alwaysIgnoredSymbols,
                alwaysDefinedSymbols: alwaysDefinedSymbols,
                alwaysDisabledSymbols: alwaysDisabledSymbols,
                undefinedSymbolValue: undefinedSymbolValue);

            return new AnalysisEngine(options);
        }

        public static AnalysisEngine FromProjects(
            IEnumerable<Project> projects,
            IEnumerable<IEnumerable<string>> additionalSymbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            Tristate undefinedSymbolValue = default(Tristate))
        {
            if (projects != null && !projects.Any())
            {
                throw new ArgumentException("Must specify at least one project");
            }

            var symbolConfigurations = projects.Select(p => p.ParseOptions.PreprocessorSymbolNames).ToArray();
            if (additionalSymbolConfigurations != null)
            {
                symbolConfigurations = symbolConfigurations.Concat(additionalSymbolConfigurations).ToArray();
            }

            var options = new Options(
                projects: projects,
                symbolConfigurations: symbolConfigurations,
                alwaysIgnoredSymbols: alwaysIgnoredSymbols,
                alwaysDefinedSymbols: alwaysDefinedSymbols,
                alwaysDisabledSymbols: alwaysDisabledSymbols,
                undefinedSymbolValue: undefinedSymbolValue);

            return new AnalysisEngine(options);
        }

        private AnalysisEngine(Options options)
        {
            _options = options;
            _expressionEvaluator = options.GetPreprocessorExpressionEvaluator();
            _expressionSimplifier = new PreprocessorExpressionSimplifier(_expressionEvaluator);
            _symbolTracker = options.GetPreprocessorSymbolTracker();
        }

        public IEnumerable<string> SpecifiedSymbols
        {
            get { return _symbolTracker.SpecifiedSymbols; }
        }

        public IEnumerable<string> UnvisitedSymbols
        {
            get { return _symbolTracker.UnvisitedSymbols; }
        }

        public IEnumerable<string> VisitedSymbols
        {
            get { return _symbolTracker.VisitedSymbols; }
        }

        /// <summary>
        /// Returns a new document in which all preprocessor expressions which evaluate to "Varying"
        /// have been simplified.
        /// </summary>
        public async Task<Document> SimplifyVaryingPreprocessorExpressions(Document document, CancellationToken cancellationToken = default(CancellationToken))
        {
            var chains = await GetConditionalRegionChains(document, cancellationToken);

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken) as CSharpSyntaxTree;
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            foreach (var chain in chains)
            {
                foreach (var region in chain.Regions)
                {
                    if (region.State == Tristate.Varying)
                    {
                        root = root.ReplaceNode(region.StartDirective, SimplifyDirectiveExpression(region.StartDirective));
                    }
                }
            }

            return document.WithSyntaxRoot(root);
        }

        /// <summary>
        /// Returns a sorted collection of <see cref="DocumentConditionalRegionInfo"/>
        /// </summary>
        public async Task<IEnumerable<DocumentConditionalRegionInfo>> GetConditionalRegionInfo(CancellationToken cancellationToken = default(CancellationToken))
        {
            var documentInfos = await Task.WhenAll(
                from document in _options.Documents
                select GetConditionalRegionInfo(document, cancellationToken));

            Array.Sort(documentInfos);

            return documentInfos;
        }

        private async Task<DocumentConditionalRegionInfo> GetConditionalRegionInfo(Document document, CancellationToken cancellationToken)
        {
            var chains = await GetConditionalRegionChains(document, cancellationToken);

            var info = new DocumentConditionalRegionInfo(document, chains);
            if (DocumentAnalyzed != null)
            {
                await DocumentAnalyzed(info, cancellationToken);
            }

            return info;
        }

        private async Task<List<ConditionalRegionChain>> GetConditionalRegionChains(Document document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chains = new List<ConditionalRegionChain>();
            var regions = new List<ConditionalRegion>();

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken) as CSharpSyntaxTree;
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            if (root.ContainsDirectives)
            {
                var currentDirective = root.GetFirstDirective(IsBranchingDirective);
                var visitedDirectives = new HashSet<DirectiveTriviaSyntax>();

                while (currentDirective != null)
                {
                    var chain = ParseConditionalRegionChain(currentDirective.GetLinkedDirectives(), visitedDirectives);
                    if (chain != null)
                    {
                        chains.Add(new ConditionalRegionChain(chain));
                    }

                    do
                    {
                        currentDirective = currentDirective.GetNextDirective(IsBranchingDirective);
                    } while (visitedDirectives.Contains(currentDirective));
                }
            }

            return chains;
        }

        private List<ConditionalRegion> ParseConditionalRegionChain(List<DirectiveTriviaSyntax> directives, HashSet<DirectiveTriviaSyntax> visitedDirectives)
        {
            DirectiveTriviaSyntax previousDirective = null;
            Tristate previousRegionState = Tristate.False;
            bool hasEnabledRegion = false;
            var chain = new List<ConditionalRegion>();

            for (int i = 0; i < directives.Count; i++)
            {
                var directive = directives[i];

                if (visitedDirectives.Contains(directive))
                {
                    // We've already visited this chain of linked directives
                    return null;
                }

                if (previousDirective != null)
                {
                    var regionState = EvaluateDirectiveExpression(previousDirective, previousRegionState);
                    previousRegionState = regionState;

                    if (regionState == Tristate.True)
                    {
                        // There can only be one always enabled region per chain
                        regionState = hasEnabledRegion ? Tristate.False : Tristate.True;
                        hasEnabledRegion = true;
                    }

                    var region = new ConditionalRegion(previousDirective, directive, chain, chain.Count, regionState);
                    chain.Add(region);
                }

                previousDirective = directive;
                visitedDirectives.Add(directive);
            }

            return chain;
        }

        private Tristate EvaluateDirectiveExpression(DirectiveTriviaSyntax directive, Tristate previousRegionState)
        {
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                    return EvaluateExpression(((IfDirectiveTriviaSyntax)directive).Condition);
                case SyntaxKind.ElifDirectiveTrivia:
                    Tristate result = EvaluateExpression(((ElifDirectiveTriviaSyntax)directive).Condition);
                    return !previousRegionState & result;
                case SyntaxKind.ElseDirectiveTrivia:
                    return !previousRegionState;
                default:
                    Debug.Assert(false);
                    return Tristate.Varying;
            }
        }

        private Tristate EvaluateExpression(ExpressionSyntax expression)
        {
            expression.Accept(_symbolTracker);
            return _expressionEvaluator.EvaluateExpression(expression);
        }

        private DirectiveTriviaSyntax SimplifyDirectiveExpression(DirectiveTriviaSyntax directive)
        {
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                    {
                        var ifDirective = (IfDirectiveTriviaSyntax)directive;
                        return ifDirective.WithCondition((ExpressionSyntax)ifDirective.Condition.Accept(_expressionSimplifier));
                    }
                case SyntaxKind.ElifDirectiveTrivia:
                    {
                        var elifDirective = (ElifDirectiveTriviaSyntax)directive;
                        return elifDirective.WithCondition((ExpressionSyntax)elifDirective.Condition.Accept(_expressionSimplifier));
                    }
                case SyntaxKind.ElseDirectiveTrivia:
                    return directive;
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        private static bool IsBranchingDirective(DirectiveTriviaSyntax directive)
        {
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                    return true;
                default:
                    return false;
            }
        }
    }
}
