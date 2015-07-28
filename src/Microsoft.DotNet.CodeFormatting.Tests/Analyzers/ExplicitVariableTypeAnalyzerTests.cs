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
            const string text = @"
class C1
{
    void M()
    {
        int x = 0;
        bool[] y = { true };  
        int[][] z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
    }
}"; 
            Verify(text, text, runFormatter: false);
        }

        [Fact]
        public void TestSimpleTypeVarDeclaration()
        {
            const string text = @"
class C1
{
    void M()
    {
        var x = 0;
        var y = new[] { true, false };  
        var z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
    }
}";
            const string expected = @"
class C1
{
    void M()
    {
        Int32 x = 0;
        Boolean[] y = new[] { true, false };  
        Int32[][] z = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } };
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestUserDefinedTypeExplicitDeclaration()
        {
            const string text = @"
class C1
{
    class U1
    {}

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        U1 x = new U1();
        U2 y = new U2();
        U3 z = U3.E1;        
    }
}";
            Verify(text, text, runFormatter: false);
        }

        [Fact]
        public void TestUserDefinedTypeVarDeclaration()
        {
            const string text = @"
class C1
{
    class U1
    {}

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        var x = new U1();
        var y = new U2();
        var z = U3.E1;         
    }
}";
            const string expected = @"
class C1
{
    class U1
    {}

    struct U2
    {}

    enum U3 { E1, E2 }

    void M()
    {
        U1 x = new U1();
        U2 y = new U2();
        U3 z = U3.E1;         
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestSimpleForEachExplicitDeclaration()
        {
            const string text = @"
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
            Verify(text, text, runFormatter: false);
        }

        [Fact]
        public void TestSimpleForEachVarDeclaration()
        {
            const string text = @"
class C1
{
    void M(string[] a1, string[][][] a2)
    {
        foreach (var element in a1)
        { }  
 
        foreach (var element in a2)
        { }
    }
}";
            const string expected = @"
class C1
{
    void M(string[] a1, string[][][] a2)
    {
        foreach (String element in a1)
        { }  
 
        foreach (String[][] element in a2)
        { }
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestAnonymousTypeDeclaration()
        {
            const string text = @"
class C1
{
    void M()
    {
        var anon = new { Name = ""Terry"", Age = 34 }; 

        foreach (var anon in new[] { new { Name = ""Terry"", Age = 34 } })
        { }
    }
}";
            Verify(text, text, runFormatter: false);
        }

        [Fact]
        public void TestVarDeclarationInUsing()
        {
            const string text = @"
class C1
{
    class U1 : IDisposable
    {
        public void Dispose(){}
    }

    void M()
    {      
        using (U1 x = new U1())
        { } 

        using (var x = new U1())
        { }         
    }
}";
            const string expected = @"
class C1
{
    class U1 : IDisposable
    {
        public void Dispose(){}
    }

    void M()
    {      
        using (U1 x = new U1())
        { } 

        using (U1 x = new U1())
        { }         
    }
}";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestVarDeclarationInSimpleLoop()
        {
            const string text = @"
class C1
{
    void M()
    {
        for (var i = 0; i < 9; ++i)
        { }
    }
}";
            const string expected = @"
class C1
{
    void M()
    {
        for (Int32 i = 0; i < 9; ++i)
        { }
    }
}";
            Verify(text, expected, runFormatter: false);
        }
    }
}
