// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class CodeFormattingTestBase
    {
        private static readonly MetadataReference s_CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference s_SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference s_SystemCollectionsImmutableReference = MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location);
        private static readonly MetadataReference s_CodeFormatterReference = MetadataReference.CreateFromFile(typeof(IFormattingEngine).Assembly.Location);

        private const string FileNamePrefix = "Test";
        private const string CSharpFileExtension = ".cs";
        private const string VBFileExtension = ".vb";
        private const string TestProjectName = "TestProject";

        protected virtual IEnumerable<MetadataReference> GetSolutionMetadataReferences()
        {
            yield return s_CorlibReference;
            yield return s_SystemCoreReference;
            yield return s_CodeFormatterReference;
            yield return s_SystemCollectionsImmutableReference;
        }

        private Workspace CreateWorkspace(string[] sources, string language = LanguageNames.CSharp)
        {
            string fileExtension = language == LanguageNames.CSharp ? CSharpFileExtension : VBFileExtension;
            var projectId = ProjectId.CreateNewId(TestProjectName);

            var workspace = new AdhocWorkspace();

            var solution = workspace
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, language)
                .AddMetadataReferences(projectId, GetSolutionMetadataReferences());

            int count = 0;
            foreach (var source in sources)
            {
                var fileName = FileNamePrefix + count + fileExtension;
                var documentId = DocumentId.CreateNewId(projectId, fileName);
                solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
                count++;
            }

            workspace.TryApplyChanges(solution);

            return workspace;
        }

        protected abstract Task<Solution> Format(Solution solution, bool runFormatter);

        private void AssertSolutionEqual(Solution expectedSolution, Solution actualSolution)
        {
            var expectedDocuments = expectedSolution.Projects.SelectMany(p => p.Documents);
            var actualDocuments = actualSolution.Projects.SelectMany(p => p.Documents);

            foreach (var expected in expectedDocuments)
            {
                var actual = actualDocuments.Where(d => d.Name == expected.Name).Single();
                var aText = actual.GetTextAsync().Result.ToString();
                var eText = expected.GetTextAsync().Result.ToString();
                if (eText != aText)
                {
                    Assert.False(true, "Document " + expected.Name + " did not match.\nActual:\n" + aText + "\nExpected:\n" + eText);
                }
            }
        }

        protected void Verify(string[] sources, string[] expected, bool runFormatter, string languageName)
        {
            var inputWorkspace = CreateWorkspace(sources, languageName);
            var expectedWorkspace = CreateWorkspace(expected, languageName);
            var actualSolution = Format(inputWorkspace.CurrentSolution, runFormatter).Result;

            if (actualSolution == null)
                Assert.False(true, "Solution is null. Test Failed.");

            AssertSolutionEqual(expectedWorkspace.CurrentSolution, actualSolution);
        }

        protected void Verify(string source, string expected, bool runFormatter = true, string languageName = LanguageNames.CSharp)
        {
            Verify(new string[] { source }, new string[] { expected }, runFormatter, languageName);
        }
    }
}
