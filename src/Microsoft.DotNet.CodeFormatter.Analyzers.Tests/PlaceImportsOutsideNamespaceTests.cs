// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Options;

using Xunit;

namespace Microsoft.DotNet.CodeFormatter.Analyzers.Tests
{
    public sealed class PlaceImportsOutsideNamespaceTests : AnalyzerFixerTestBase
    {
        public PlaceImportsOutsideNamespaceTests()
        {
            OptionsHelper.GetPropertiesImplementation = (analyzerOptions) =>
            {
                PropertyBag properties = CreatePolicyThatDisablesAllAnalysis();
                properties.SetProperty(OptionsHelper.BuildDefaultEnabledProperty(PlaceImportsOutsideNamespaceAnalyzer.AnalyzerName), true);
                return properties;
            };
        }

        [Fact]
        public void PlaceImportsOutsideNamespace_Simple()
        {
            string input = @"
namespace N1
{
    using System.Runtime.InteropServices;
    using AnotherUnreferencedNamespace;
    using System.Threading;
    using System;
    using System.IO;
    using System.Xml;

    public class Test
    {
        public static void Main()
        {
            Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
        }
    }
}
";

            string expected = @"
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.IO;

namespace N1
{
    using AnotherUnreferencedNamespace;
    using System.Xml;

    public class Test
    {
        public static void Main()
        {
            Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
        }
    }
}
";
            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void PlaceImportsOutsideNamespace_AddToExisting()
        {
            string input = @"
using System.Runtime.InteropServices;

namespace N1
{
    using System;
    using System.IO;

    public class Test
    {
        public static void Main()
        {
            Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
        }
    }
}
";

            string expected = @"
using System.Runtime.InteropServices;
using System;
using System.IO;

namespace N1
{
    public class Test
    {
        public static void Main()
        {
            Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
        }
    }
}
";
            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void PlaceImportsOutsideNamespace_Trivia()
        {
            string input = @"
// Copyright Header

namespace N1
{
    using System;
    using System.IO; //Some comments

    public class Test
    {
        public static void Main()
        {
            Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
        }
    }
}
";

            string expected = @"
// Copyright Header

using System;
using System.IO; //Some comments

namespace N1
{
    public class Test
    {
        public static void Main()
        {
            Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
        }
    }
}
";
            Verify(input, expected, runFormatter: false);
        }
    }
}
