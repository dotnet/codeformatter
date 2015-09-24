// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.CodeFormatting.Tests;
using Microsoft.DotNet.CodeFormatting;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers.Tests
{
    public abstract class AnalyzerFixerTestBase : CodeFormattingTestBase
    {
        private IFormattingEngine _engine;

        private IFormattingEngine Engine
        {
            get
            {
                if (_engine == null)
                {
                    _engine = FormattingEngine.Create(
                        new Assembly[] {
                            typeof(FormattingEngine).Assembly,
                            typeof(OptimizeNamespaceImportsAnalyzer).Assembly}
                        );
                }

                return _engine;
            }
        }

        protected PropertyBag CreatePolicyThatDisablesAllAnalysis()
        {
            PropertyBag propertyBag = OptionsHelper.BuildDefaultPropertyBag();

            foreach (string analyzerName in OptionsHelper.AllAnalyzerNames)
            {
                propertyBag.SetProperty(OptionsHelper.BuildDefaultEnabledProperty(analyzerName), false);
            }
            return propertyBag;
        }

        protected override async Task<Solution> Format(Solution solution, bool runFormatter)
        {
            Workspace workspace = solution.Workspace;
            await Engine.FormatSolutionAsync(solution, useAnalyzers: true, cancellationToken: default(CancellationToken)).ConfigureAwait(false);
            return workspace.CurrentSolution;
        }
    }
}
