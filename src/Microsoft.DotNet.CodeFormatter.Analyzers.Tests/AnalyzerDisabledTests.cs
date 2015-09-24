// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Options;

using Xunit;

namespace Microsoft.DotNet.CodeFormatter.Analyzers.Tests
{
    public sealed class AnalyzerDisabledTests : AnalyzerFixerTestBase
    {
        public AnalyzerDisabledTests()
        {
            OptionsHelper.GetPropertiesImplementation = (analyzerOptions) =>
            {
                PropertyBag properties = CreatePolicyThatDisablesAllAnalysis();
                return properties;
            };
        }

        [Fact]
        public void AnalyzerDisabled_OptimizeNamespaceImports()
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
            string expected = input;
            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void AnalyzerDisabled_ProvideExplicitVariableType()
        {
            const string input = @"
class C1
{
    bool[] T() 
    {
        return new[] { true };
    }

    void M(int a, bool[] b)
    {
        var x = a;
        var y = T();  
        var z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
    }
}";
            const string expected = input;
            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void AnalyzerDisabled_UnwrittenWritableField()
        {
            const string input = @"
class C
{
    private READONLY int read;
}
";
            const string expected = input;
            Verify(input, expected, runFormatter: false);
        }

        [Fact]
        public void AnalyzerDisabled_ExplicitThis()
        {
            const string input = ExplicitThisAnalyzerTests.TestFieldAssignment_Input;
            const string expected = input;
            Verify(input, expected, runFormatter: false);
        }
    }
}