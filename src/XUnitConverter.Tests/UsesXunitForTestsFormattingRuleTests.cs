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

namespace XUnitConverterTests
{
    public class UsesXunitForTestsFormattingRuleTests
    {
        private static readonly MetadataReference s_MSTestReference = MetadataReference.CreateFromAssembly(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly);
        private static readonly MetadataReference s_XunitReference = MetadataReference.CreateFromAssembly(typeof(FactAttribute).Assembly);

        private CustomWorkspace CreateWorkspace()
        {
            return null;
        }

        private async Task<Solution> RunConverter(Project project, bool runFormatter)
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

            return solution;
        }

        /*
        protected override IEnumerable<MetadataReference> GetSolutionMetadataReferences()
        {
            return base.GetSolutionMetadataReferences()
                .Concat(new[] {
                    s_MSTestReference,
                    s_XunitReference
                });
        }
        */

        [Fact]
        public void TestUpdatesUsingStatements()
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
            Verify(text, expected);
        }

        [Fact]
        public void TestUpdatesUsingStatementsWithIfDefs()
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
            Verify(text, expected);
        }

        [Fact]
        public void TestRemovesTestClassAttributes()
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
            Verify(text, expected);
        }

        [Fact]
        public void TestUpdatesTestMethodAttributes()
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
            Verify(text, expected);
        }

        [Fact]
        public void TestUpdatesAsserts()
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
            Verify(text, expected);
        }
    }
}
