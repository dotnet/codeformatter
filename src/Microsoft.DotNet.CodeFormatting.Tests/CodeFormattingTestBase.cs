// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class CodeFormattingTestBase
    {
        private static readonly MetadataReference s_CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference s_SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
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

        /// <summary>
        /// Verify that the input text
        /// </summary>
        /// <param name="resourceNameStem"></param>
        /// <param name="runFormatter"></param>
        /// <param name="languageName"></param>
        protected void Verify(string resourceNameStem, bool runFormatter = true, string languageName = LanguageNames.CSharp)
        {
            var source = GetInputData(resourceNameStem);
            var expected = GetExpectedData(resourceNameStem);

            Verify(source, expected, runFormatter, languageName);
        }

        private string GetInputData(string resourceNameStem)
        {
            return GetResourceData("Input", resourceNameStem);
        }

        private string GetExpectedData(string resourceNameStem)
        {
            return GetResourceData("Expected", resourceNameStem);
        }

        private string GetResourceData(string dataCategory, string resourceNameStem)
        {
            Assembly thisAssembly = Assembly.GetExecutingAssembly();

            // Strip off the conventional test class name suffix so that, for example,
            // "ExplicitThisRuleTests" becomes "ExplicitThis".
            string testCategory = GetType().Name;
            int suffixIndex = testCategory.IndexOf("RuleTests");
            if (suffixIndex == -1)
            {
                suffixIndex = testCategory.IndexOf("AnalyzerTests");
            }

            if (suffixIndex > 0)
            {
                testCategory = testCategory.Substring(0, suffixIndex);
            }

            string resourceName =
                string.Join(".", thisAssembly.GetName().Name, "TestData", testCategory, dataCategory, resourceNameStem)
                + CSharpFileExtension;

            Stream resourceStream = thisAssembly.GetManifestResourceStream(resourceName);
            TextReader reader = new StreamReader(resourceStream);

            return reader.ReadToEnd();
        }
    }

    // TODO: Pull RuleTestBase and its three derived classes into separate file RuleTestBase.cs.
    public abstract class RuleTestBase : CodeFormattingTestBase
    {
        protected override async Task<Solution> Format(Solution solution, bool runFormatter)
        {
            var documentIds = solution.Projects.SelectMany(p => p.DocumentIds);

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);
                document = await RewriteDocumentAsync(document).ConfigureAwait(false);
                if (runFormatter)
                {
                    document = await Formatter.FormatAsync(document).ConfigureAwait(false);
                }

                solution = document.Project.Solution;
            }

            return solution;
        }

        protected abstract Task<Document> RewriteDocumentAsync(Document document);
    }

    public abstract class SyntaxRuleTestBase : RuleTestBase
    {
        internal abstract ISyntaxFormattingRule Rule
        {
            get;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync();
            syntaxRoot = Rule.Process(syntaxRoot, document.Project.Language);
            return document.WithSyntaxRoot(syntaxRoot);
        }
    }

    public abstract class LocalSemanticRuleTestBase : RuleTestBase
    {
        internal abstract ILocalSemanticFormattingRule Rule
        {
            get;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync();
            syntaxRoot = await Rule.ProcessAsync(document, syntaxRoot, CancellationToken.None);
            return document.WithSyntaxRoot(syntaxRoot);
        }
    }

    public abstract class GlobalSemanticRuleTestBase : RuleTestBase
    {
        internal abstract IGlobalSemanticFormattingRule Rule
        {
            get;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var solution = await Rule.ProcessAsync(document, await document.GetSyntaxRootAsync(), CancellationToken.None);
            return solution.GetDocument(document.Id);
        }
    }
}
