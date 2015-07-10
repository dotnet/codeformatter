// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading; 
using System.Threading.Tasks; 

using Microsoft.CodeAnalysis; 

namespace Microsoft.DotNet.CodeFormatting.Tests
{ 
    public abstract class AnalyzerFixerTestBase : CodeFormattingTestBase 
    { 
        private IFormattingEngine _engine; 

        protected AnalyzerFixerTestBase(IFormattingEngine engine)
        { 
            _engine = engine; 
        } 

        protected override async Task<Solution> Format(Solution solution, bool runFormatter)
        { 
            Workspace workspace = solution.Workspace; 
            await _engine.FormatSolutionAsync(solution, /* useAnalyzers = */ true, default(CancellationToken)).ConfigureAwait(false); 
            return workspace.CurrentSolution; 
        } 
    } 
} 
