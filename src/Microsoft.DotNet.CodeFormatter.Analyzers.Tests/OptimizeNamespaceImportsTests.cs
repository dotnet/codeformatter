// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Options;

using Xunit;

namespace Microsoft.DotNet.CodeFormatter.Analyzers.Tests
{
    public sealed class OptimizeNamespaceImportsTests : AnalyzerFixerTestBase
    {
        public OptimizeNamespaceImportsTests()
        {
            OptionsHelper.GetPropertiesImplementation = (analyzerOptions) => 
            {
                PropertyBag properties = CreatePolicyThatDisablesAllAnalysis();
                properties.SetProperty(OptionsHelper.BuildDefaultEnabledProperty(OptimizeNamespaceImportsAnalyzer.AnalyzerName), true);
                return properties;
            };
        }

        [Fact]
        public void OptimizeNamespaceImports_SimpleRemoveUnusedImports()
        {
            string input = @"
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
";

            string expected = @"
using System;

public class Test
{
    public static void Main()
    {
        Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
    }
}
";
            Verify(input, expected, runFormatter: false);
        }
        [Fact]
        public void OptimizeNamespaceImports_DisregardCodeWithIfDirectives()
        {
            string input =
            @"
                using System;
                using System.Xml;

                public class Test
                {
                    public static void Main()
                    {
                        Console.WriteLine(""Calling Console.WriteLine, a dependency on System namespace."");
                    }
#if XML_FEATURE
                    public virtual void XmlDependency(XmlReader reader(){}
#endif
                }
            ";

            string expected = input;

            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void OptimizeNamespaceImports_PrecededByComment()
        {
            string input = @"
// Copyright notice
using System;
using System.Xml;

namespace MyNamespace 
{
    public class Test {}
}";

            string expected = @"
// Copyright notice

namespace MyNamespace 
{
    public class Test {}
}";

            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void OptimizeNamespaceImports_WrappedByComments()
        {
            string input = @"
// Preceding comment

using System.Xml;
// Following comment
namespace MyNamespace 
{
        public class Test {}
}";

            string expected = @"
// Preceding comment

// Following comment
namespace MyNamespace 
{
        public class Test {}
}";

            Verify(input, expected, runFormatter: false);
        }
    }
}
