// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.DotNet.DeadRegionAnalysis.Tests
{
    public class TestBase
    {
        private static readonly MetadataReference s_CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference s_SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);

        private const string FileNamePrefix = "Test";
        private const string CSharpFileExtension = ".cs";
        private const string VBFileExtension = ".vb";
        private const string TestProjectName = "TestProject";

        protected static IEnumerable<int> GetPositions(string markup, out string source)
        {
            var positions = new List<int>();
            source = markup;

            while (true)
            {
                int pos = source.IndexOf("$$");

                if (pos == -1)
                {
                    break;
                }

                source = source.Remove(pos, 2);
                positions.Add(pos);
            }

            return positions;
        }

        protected virtual IEnumerable<MetadataReference> GetSolutionMetadataReferences()
        {
            yield return s_CorlibReference;
            yield return s_SystemCoreReference;
        }

        protected Solution CreateSolution(string[] sources, string[] preprocessorSymbols = null, string language = LanguageNames.CSharp)
        {
            string fileExtension = language == LanguageNames.CSharp ? CSharpFileExtension : VBFileExtension;
            var projectId = ProjectId.CreateNewId(TestProjectName);

            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, language)
                .AddMetadataReferences(projectId, GetSolutionMetadataReferences());

            if (preprocessorSymbols != null)
            {
                var project = solution.Projects.Single();
                project = project.WithParseOptions(
                    ((CSharpParseOptions)project.ParseOptions).WithPreprocessorSymbols(preprocessorSymbols));

                solution = project.Solution;
            }

            int count = 0;
            foreach (var source in sources)
            {
                var fileName = FileNamePrefix + count + fileExtension;
                var documentId = DocumentId.CreateNewId(projectId, fileName);
                solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
            }

            return solution;
        }

        protected async static Task AssertSolutionEqual(Solution expectedSolution, Solution actualSolution)
        {
            var expectedDocuments = expectedSolution.Projects.SelectMany(p => p.Documents);
            var actualDocuments = actualSolution.Projects.SelectMany(p => p.Documents);

            foreach (var expected in expectedDocuments)
            {
                var actual = actualDocuments.Where(d => d.Name == expected.Name).Single();
                var aText = (await actual.GetTextAsync().ConfigureAwait(false)).ToString();
                var eText = (await expected.GetTextAsync().ConfigureAwait(false)).ToString();
                Assert.Equal(eText, aText);
            }
        }

        protected static async Task<List<DirectiveTriviaSyntax>> GetDirectivesFromPositions(Document document, IEnumerable<int> positions, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var directives = new List<DirectiveTriviaSyntax>();

            foreach (var position in positions)
            {
                var directive = syntaxRoot.FindNode(new TextSpan(position, 1), findInsideTrivia: true) as DirectiveTriviaSyntax;
                if (directive == null)
                {
                    var sourceText = await document.GetTextAsync(cancellationToken);
                    var line = sourceText.Lines.GetLineFromPosition(position);
                    Assert.False(true, string.Format("No directive found at document position {0}, line {1}: {2}", position, line.LineNumber, line.ToString()));
                }

                directives.Add(directive);
            }

            return directives;
        }
    }
}
