// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using System.Threading;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace XUnitConverterTests
{
    public class UsesXunitForTestsFormattingRuleTests
    {
        private static readonly MetadataReference s_CorlibReference = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
        private static readonly MetadataReference s_SystemCoreReference = MetadataReference.CreateFromAssembly(typeof(Enumerable).Assembly);
        private static readonly MetadataReference s_MSTestReference = MetadataReference.CreateFromAssembly(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly);
        private static readonly MetadataReference s_XunitReference = MetadataReference.CreateFromAssembly(typeof(FactAttribute).Assembly);

        private async Task<Project> RunConverter(Project project, bool runFormatter)
        {
            var xunitConverter = new XUnitConverter.XUnitConverter();
            var solution = await xunitConverter.ProcessAsync(project, CancellationToken.None);

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

            var solution = new CustomWorkspace()
                .CurrentSolution
                .AddProject(projectId, testProjectName, testProjectName, LanguageNames.CSharp)
                .AddMetadataReferences(projectId, references);

            var fileName = "File.cs";
            var documentId = DocumentId.CreateNewId(projectId, fileName);
            solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
            return solution.GetProject(projectId);
        }

        private async Task Verify(string text, string expected, bool runFormatter = true)
        {
            var project = CreateSolution(text);
            project = await RunConverter(project, runFormatter);
            var actual = await project.Documents.Single().GetTextAsync(CancellationToken.None);
            Assert.Equal(expected, actual.ToString());
        }

        [Fact]
        public async Task TestUpdatesUsingStatements()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestUpdatesUsingStatementsWithIfDefs()
        {
            var text = @"
using System;
#if NETFX_CORE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#elif PORTABLE_TESTS
using Microsoft.Bcl.Testing;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endif
namespace System.Composition.UnitTests
{
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestRemovesTestClassAttributes()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
    [TestClass]
    public class MyTestClass
    {
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestUpdatesTestMethodAttributes()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        [TestMethod]
        public void MyTestMethod()
        {
        }
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        [Fact]
        public void MyTestMethod()
        {
        }
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestUpdatesAsserts()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        public void MyTestMethod()
        {
            object obj = new object();

            Assert.AreEqual(1, 1);
            Assert.AreNotEqual(1, 2);
            Assert.IsNull(null);
            Assert.IsNotNull(obj);
            Assert.AreSame(obj, obj);
            Assert.AreNotSame(obj, new object());
            Assert.IsTrue(true);
            Assert.IsFalse(false);
            Assert.IsInstanceOfType(string.Empty, typeof(String));
        }
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        public void MyTestMethod()
        {
            object obj = new object();

            Assert.Equal(1, 1);
            Assert.NotEqual(1, 2);
            Assert.Null(null);
            Assert.NotNull(obj);
            Assert.Same(obj, obj);
            Assert.NotSame(obj, new object());
            Assert.True(true);
            Assert.False(false);
            Assert.IsAssignableFrom(typeof(String), string.Empty);
        }
    }
}
";
            await Verify(text, expected);
        }
    }
}
