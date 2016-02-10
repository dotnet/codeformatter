// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public partial class AnalysisEngine
    {
        private Options _options;
        private CompositePreprocessorExpressionEvaluator _expressionEvaluator;
        private PreprocessorExpressionSimplifier _expressionSimplifier;
        private PreprocessorSymbolTracker _symbolTracker;

        public event Func<AnalysisEngine, DocumentConditionalRegionInfo, CancellationToken, Task> DocumentAnalyzed;

        public static async Task<AnalysisEngine> FromFilePaths(
            IEnumerable<string> filePaths,
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            Tristate undefinedSymbolValue = default(Tristate),
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (filePaths == null || !filePaths.Any())
            {
                throw new ArgumentException("Must specify at least one file path");
            }

            IEnumerable<string> sourcePaths = null;
            Project[] projects = null;

            var firstFileExt = Path.GetExtension(filePaths.First());
            if (firstFileExt.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                projects = await Task.WhenAll(from path in filePaths select MSBuildWorkspace.Create().OpenProjectAsync(path, cancellationToken));
            }
            else
            {
                sourcePaths = filePaths;
            }

            var options = new Options(
                projects: projects,
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
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            Tristate undefinedSymbolValue = default(Tristate))
        {
            if (projects != null && !projects.Any())
            {
                throw new ArgumentException("Must specify at least one project");
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

        public IAnalysisLogger Logger
        {
            get { return _options.Logger; }
            set { _options.Logger = value; }
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

            var directivesToReplace = chains
                .SelectMany(c => c.Regions)
                .Where(r => r.State == Tristate.Varying)
                .Select(r => r.StartDirective);

            root = root.ReplaceNodes(directivesToReplace, (original, rewritten) => SimplifyDirectiveExpression(rewritten));

            return document.WithSyntaxRoot(root);
        }

        /// <summary>
        /// Returns a sorted collection of <see cref="DocumentConditionalRegionInfo"/>
        /// </summary>
        public async Task<ImmutableArray<DocumentConditionalRegionInfo>> GetConditionalRegionInfo(CancellationToken cancellationToken = default(CancellationToken))
        {
            var documentInfos = await Task.WhenAll(
                from document in _options.Documents
                select GetConditionalRegionInfo(document, cancellationToken));

            Array.Sort(documentInfos);

            return ImmutableArray.Create<DocumentConditionalRegionInfo>(documentInfos);
        }

        private async Task<DocumentConditionalRegionInfo> GetConditionalRegionInfo(Document document, CancellationToken cancellationToken)
        {
            var chains = await GetConditionalRegionChains(document, cancellationToken);

            var info = new DocumentConditionalRegionInfo(document, chains);
            if (DocumentAnalyzed != null)
            {
                await DocumentAnalyzed(this, info, cancellationToken);
            }

            return info;
        }

        private async Task<ImmutableArray<ConditionalRegionChain>> GetConditionalRegionChains(Document document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chains = ImmutableArray.CreateBuilder<ConditionalRegionChain>();
            var regions = ImmutableArray.CreateBuilder<ConditionalRegion>();

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken) as CSharpSyntaxTree;
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            if (root.ContainsDirectives)
            {
                var currentDirective = root.GetFirstDirective(IsBranchingDirective);
                var visitedDirectives = new HashSet<DirectiveTriviaSyntax>();

                while (currentDirective != null)
                {
                    var chain = ParseConditionalRegionChain(currentDirective.GetLinkedDirectives().ToImmutableArray(), visitedDirectives);
                    if (!chain.IsDefault)
                    {
                        chains.Add(new ConditionalRegionChain(chain));
                    }

                    do
                    {
                        currentDirective = currentDirective.GetNextDirective(IsBranchingDirective);
                    } while (visitedDirectives.Contains(currentDirective));
                }
            }

            return chains.ToImmutable();
        }

        private ImmutableArray<ConditionalRegion> ParseConditionalRegionChain(IList<DirectiveTriviaSyntax> directives, HashSet<DirectiveTriviaSyntax> visitedDirectives)
        {
            DirectiveTriviaSyntax previousDirective = null;
            Tristate previousRegionState = Tristate.False;
            bool hasEnabledRegion = false;
            var chain = ImmutableArray.CreateBuilder<ConditionalRegion>();

            for (int i = 0; i < directives.Count; i++)
            {
                var directive = directives[i];

                if (visitedDirectives.Contains(directive))
                {
                    // We've already visited this chain of linked directives
                    return default(ImmutableArray<ConditionalRegion>);
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

                    var region = new ConditionalRegion(previousDirective, directive, regionState);
                    chain.Add(region);
                }

                previousDirective = directive;
                visitedDirectives.Add(directive);
            }

            return chain.ToImmutable();
        }

        private Tristate EvaluateDirectiveExpression(DirectiveTriviaSyntax directive, Tristate previousRegionState)
        {
            switch (directive.Kind())
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
            switch (directive.Kind())
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
            switch (directive.Kind())
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
