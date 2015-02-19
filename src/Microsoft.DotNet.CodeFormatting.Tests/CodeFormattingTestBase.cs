// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.CodeFormatting;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class CodeFormattingTestBase
    {
        private static readonly MetadataReference s_CorlibReference = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
        private static readonly MetadataReference s_SystemCoreReference = MetadataReference.CreateFromAssembly(typeof(Enumerable).Assembly);
        private static readonly MetadataReference s_CodeFormatterReference = MetadataReference.CreateFromAssembly(typeof(IFormattingEngine).Assembly);

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

        private Solution CreateSolution(string[] sources, string language = LanguageNames.CSharp)
        {
            string fileExtension = language == LanguageNames.CSharp ? CSharpFileExtension : VBFileExtension;
            var projectId = ProjectId.CreateNewId(TestProjectName);

            var solution = new AdhocWorkspace()
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

            return solution;
        }

        private async Task<Solution> Format(Solution solution, bool runFormatter)
        {
            var documentIds = solution.Projects.SelectMany(p => p.DocumentIds);

            foreach (var id in documentIds)
            {
                var document = solution.GetDocument(id);
                document = await RewriteDocumentAsync(document);
                if (runFormatter)
                {
                    document = await Formatter.FormatAsync(document);
                }

                solution = document.Project.Solution;
            }

            return solution;
        }

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

        protected abstract Task<Document> RewriteDocumentAsync(Document document);

        protected void Verify(string[] sources, string[] expected, bool runFormatter, string languageName)
        {
            var inputSolution = CreateSolution(sources, languageName);
            var expectedSolution = CreateSolution(expected, languageName);
            var actualSolution = Format(inputSolution, runFormatter).Result;

            if (actualSolution == null)
                Assert.False(true, "Solution is null. Test Failed.");

            AssertSolutionEqual(expectedSolution, actualSolution);
        }

        protected void Verify(string source, string expected, bool runFormatter = true, string languageName = LanguageNames.CSharp)
        {
            Verify(new string[] { source }, new string[] { expected }, runFormatter, languageName);
        }
    }

    public abstract class SyntaxRuleTestBase : CodeFormattingTestBase
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

    public abstract class LocalSemanticRuleTestBase : CodeFormattingTestBase
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

    public abstract class GlobalSemanticRuleTestBase : CodeFormattingTestBase
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
