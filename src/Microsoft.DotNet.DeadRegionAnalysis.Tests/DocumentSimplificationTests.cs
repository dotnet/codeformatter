// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class DocumentSimplificationTests : TestBase
    {
        [Fact]
        public void Simplify()
        {
            string source = @"
#if true && varying // To be simplified
#endif

#if false || varying // To be simplified
#endif

#if true && false // Left alone
#endif
";
            string expected = @"
#if varying // To be simplified
#endif

#if varying // To be simplified
#endif

#if true && false // Left alone
#endif
";
            Verify(source, expected);
        }

        protected void Verify(string source, string expected)
        {
            var inputSolution = CreateSolution(new[] { source });
            var expectedSolution = CreateSolution(new[] { expected });

            var engine = AnalysisEngine.FromProjects(inputSolution.Projects, alwaysIgnoredSymbols: new[] { "varying" });

            var document = inputSolution.Projects.Single().Documents.Single();
            var actualSolution = engine.SimplifyVaryingPreprocessorExpressions(document).Result.Project.Solution;

            AssertSolutionEqual(expectedSolution, actualSolution);
        }
    }
}
