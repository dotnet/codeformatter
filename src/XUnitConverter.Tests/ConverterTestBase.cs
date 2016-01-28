// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XUnitConverter;

namespace XUnitConverter.Tests
{
    public abstract class ConverterTestBase
    {
        private static readonly MetadataReference s_CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference s_SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference s_MSTestReference = MetadataReference.CreateFromFile(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly.Location);
        private static readonly MetadataReference s_XunitReference = MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);

        protected abstract ConverterBase CreateConverter();

        private async Task<Project> RunConverter(Project project, bool runFormatter)
        {
            var converter = CreateConverter();
            var solution = await converter.ProcessAsync(project, CancellationToken.None);

            if (runFormatter)
            {
                foreach (var id in project.DocumentIds)
                {
                    var document = solution.GetDocument(id);
                    document = await Formatter.FormatAsync(document);
                    solution = document.Project.Solution;
                }
            }

            return solution.GetProject(project.Id); ;
        }

        private Project CreateSolution(string source)
        {
            var testProjectName = "Test";
            var projectId = ProjectId.CreateNewId(testProjectName);

            var references = new[]
                {
                    s_CorlibReference,
                    s_SystemCoreReference,
                    s_MSTestReference,
                    s_XunitReference
                };

            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, testProjectName, testProjectName, LanguageNames.CSharp)
                .AddMetadataReferences(projectId, references);

            var fileName = "File.cs";
            var documentId = DocumentId.CreateNewId(projectId, fileName);
            solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
            return solution.GetProject(projectId);
        }

        protected async Task Verify(string text, string expected, bool runFormatter = true)
        {
            var project = CreateSolution(text);
            project = await RunConverter(project, runFormatter);
            var actual = await project.Documents.Single().GetTextAsync(CancellationToken.None);
            Assert.Equal(expected, actual.ToString());
        }
    }
}
