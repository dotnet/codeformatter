// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.CodeFormatting.Analyzers;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitVariableTypeAnalyzerTests : AnalyzerFixerTestBase
    {
        public ExplicitVariableTypeAnalyzerTests()
        {
            DisableAllDiagnostics();
            EnableDiagnostic(ExplicitVariableTypeAnalyzer.DiagnosticId);
        }
        [Fact]
        public void TestSimpleTypeExplicitDeclaration()
        {
            var text = @"
class C1
{
    void M()
    {
        int x = 0;
        bool[] y = { true };  
        int[][] z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
    }
}";
            var expected = @"
class C1
{
    void M()
    {
        int x = 0;
        bool[] y = { true };  
        int[][] z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestSimpleTypeVarDeclaration()
        {
            var text = @"
class C1
{
    void M()
    {
        var x = 0;
        var y = new[] { true, false };  
        var z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
        var anon = new { Name = ""Terry"", Age = 34 };
    }
}";
            var expected = @"
class C1
{
    void M()
    {
        Int32 x = 0;
        Boolean[] y = new[] { true, false };  
        Int32[][] z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
        var anon = new { Name = ""Terry"", Age = 34 };
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestUserDefinedTypeExplicitDeclaration()
        {
            var text = @"
class C1
{
    class U1 : IDisposable
    {
        public void Dispose(){}
    }

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        U1 x = new U1();
        U2 y = new U2();
        U3 z = U3.E1;
        using (U1 x = new U1())
        { }         
    }
}";
            var expected = @"
class C1
{
    class U1 : IDisposable
    {
        public void Dispose(){}
    }

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        U1 x = new U1();
        U2 y = new U2();
        U3 z = U3.E1;
        using (U1 x = new U1())
        { }         
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestUserDefinedTypeVarDeclaration()
        {
            var text = @"
class C1
{
    class U1 : IDisposable
    {
        public void Dispose(){}
    }

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        var x = new U1();
        var y = new U2();
        var z = U3.E1;
        using (var x = new U1())
        { }         
    }
}";
            var expected = @"
class C1
{
    class U1 : IDisposable
    {
        public void Dispose(){}
    }

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        U1 x = new U1();
        U2 y = new U2();
        U3 z = U3.E1;
        using (U1 x = new U1())
        { }         
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestSimpleForEachExplicitDeclaration()
        {
            var text = @"
class C1
{
    void M(string[] a1, string[][] a2)
    {
        foreach (string element in a1)
        { }  
 
        foreach (string[] element in a2)
        { }
    }
}";
            var expected = @"
class C1
{
    void M(string[] a1, string[][] a2)
    {
        foreach (string element in a1)
        { }  
 
        foreach (string[] element in a2)
        { }
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestSimpleForEachVarDeclaration()
        {
            var text = @"
class C1
{
    void M(string[] a1, string[][][] a2)
    {
        foreach (var element in a1)
        { }  
 
        foreach (var element in a2)
        { }

        foreach (var anon in new[] { new { Name = ""Terry"", Age = 34 } })
        { }
    }
}";
            var expected = @"
class C1
{
    void M(string[] a1, string[][][] a2)
    {
        foreach (String element in a1)
        { }  
 
        foreach (String[][] element in a2)
        { }

        foreach (var anon in new[] { new { Name = ""Terry"", Age = 34 } })
        { }
    }
}";
            Verify(text, expected, runFormatter: false);
        }
    }
}
