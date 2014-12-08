using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.CodeFormatting;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class CodeFormattingTestBase
    {
        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
        private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromAssembly(typeof(Enumerable).Assembly);
        private static readonly MetadataReference CodeFormatterReference = MetadataReference.CreateFromAssembly(typeof(IFormattingEngine).Assembly);

        private const string FileNamePrefix = "Test";
        private const string CSharpFileExtension = ".cs";
        private const string VBFileExtension = ".vb";
        private const string TestProjectName = "TestProject";

        internal abstract IFormattingRule GetFormattingRule();

        internal static IFormattingRule GetDefaultVSFormatter()
        {
            return new Rules.IsFormattedFormattingRule();
        }

        internal static Solution CreateSolution(string[] sources, string language = LanguageNames.CSharp)
        {
            string fileExtension = language == LanguageNames.CSharp ? CSharpFileExtension : VBFileExtension;
            var projectId = ProjectId.CreateNewId(TestProjectName);

            var solution = new CustomWorkspace()
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, language)
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemCoreReference)
                .AddMetadataReference(projectId, CodeFormatterReference);

            int count = 0;
            foreach(var source in sources)
            {
                var fileName = FileNamePrefix + count + fileExtension;
                var documentId = DocumentId.CreateNewId(projectId, fileName);
                solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
            }

            return solution;
        }

        internal static async Task<Solution> Format(Solution solution, IFormattingRule rule)
        {
            var documentIds = solution.Projects.SelectMany(p => p.DocumentIds);

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);
                var newDocument = await RewriteDocumentAsync(document, rule);
                solution = newDocument.Project.Solution;
            }

            return solution;
        }

        internal static async Task<Document> RewriteDocumentAsync(Document document, IFormattingRule rule)
        {
            document = await rule.ProcessAsync(document, CancellationToken.None);
            return await GetDefaultVSFormatter().ProcessAsync(document, CancellationToken.None);
        }

        internal static void AssertSolutionEqual(Solution expectedSolution, Solution actualSolution)
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

        internal void Verify(string[] sources, string[] expected, IFormattingRule rule)
        {
            var inputSolution = CreateSolution(sources);
            var expectedSolution = CreateSolution(expected);
            var actualSolution = Format(inputSolution, rule).Result;

            if (actualSolution == null)
                Assert.False(true, "Solution is null. Test Failed.");

            AssertSolutionEqual(expectedSolution, actualSolution);
        }

        internal void Verify(string[] source, string[] expected)
        {
            Verify(source, expected, GetFormattingRule());
        }

        internal void Verify(string source, string expected)
        {
            Verify(new string[] { source }, new string[] { expected });
        }
    }
}
