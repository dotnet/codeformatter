// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitVisibilityRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.ExplicitVisibilityRule(); }
        }

        [Fact]
        public void TestTypeVisibility()
        {
            var text = @"
class C1 { } 
sealed partial class C2 { } 
struct S1 { }
interface I1 { } 
enum E1 { }
delegate void D1() { } 
";

            var expected = @"
internal class C1 { } 
internal sealed partial class C2 { } 
internal struct S1 { }
internal interface I1 { } 
internal enum E1 { }
internal delegate void D1() { } 
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestNestedTypeVisibility()
        {
            var text = @"
class C
{
    class C { } 
    struct S { } 
    interface I { } 
    enum E { } 
    delegate void D() { } 
}
";

            var expected = @"
internal class C
{
    private class C { } 
    private struct S { } 
    private interface I { } 
    private enum E { } 
    private delegate void D() { } 
}
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestMethodVisibility()
        {
            var text = @"
internal class C
{
    void M1();
    internal void M2();
}

internal struct S
{
    void M1();
    internal void M2();
}

internal interface I
{
    void M1();
    void M2();
}
";

            var expected = @"
internal class C
{
    private void M1();
    internal void M2();
}

internal struct S
{
    private void M1();
    internal void M2();
}

internal interface I
{
    void M1();
    void M2();
}
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestExplicitInterfaceImplementation()
        {
            var text = @"
interface I1
{
    int this[int index] { get; set; }
    int Prop { get; set; }
    void M();
    event EventHandler E;
}

class C : I1
{
    int I1.Prop
    {
        get { return 0; }
        set { }
    }
    int I1.this[int index] 
    {
        get { return 0; }
        set { } 
    }
    void I1.M() { } 
    event EventHandler I1.E;
    void M() { }
}
";

            var expected = @"
internal interface I1
{
    int this[int index] { get; set; }
    int Prop { get; set; }
    void M();
    event EventHandler E;
}

internal class C : I1
{
    int I1.Prop
    {
        get { return 0; }
        set { }
    }
    int I1.this[int index] 
    {
        get { return 0; }
        set { } 
    }
    void I1.M() { } 
    event EventHandler I1.E;
    private void M() { }
}
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldImplementation()
        {
            var text = @"
class C
{
    const int Max;
    int Field1;
    public int Field2;
    event EventHandler E1;
    public event EventHandler E2;
}

struct C
{
    const int Max;
    int Field1;
    public int Field2;
    event EventHandler E1;
    public event EventHandler E2;
}
";

            var expected = @"
internal class C
{
    private const int Max;
    private int Field1;
    public int Field2;
    private event EventHandler E1;
    public event EventHandler E2;
}

internal struct C
{
    private const int Max;
    private int Field1;
    public int Field2;
    private event EventHandler E1;
    public event EventHandler E2;
}
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestConstructor()
        {
            var text = @"
class C
{
    static C() { } 
    C() { } 
    internal C(int p) { } 
}

struct S
{
    static S() { } 
    S(int p) { } 
    internal S(int p1, int p2) { } 
}
";
            var expected = @"
internal class C
{
    static C() { } 
    private C() { } 
    internal C(int p) { } 
}

internal struct S
{
    static S() { } 
    private S(int p) { } 
    internal S(int p1, int p2) { } 
}
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestPrivateFields()
        {
            var text = @"
using System;
class T
{
    static int x;
    private static int y;
    // some trivia
    protected internal int z;
    // some trivia
    int k = 1, s = 2;
    // some trivia
}";
            var expected = @"
using System;
internal class T
{
    private static int x;
    private static int y;
    // some trivia
    protected internal int z;
    // some trivia
    private int k = 1, s = 2;
    // some trivia
}";
            Verify(text, expected);
        }

        [Fact]
        public void LonePartialType()
        {
            var text = @"
partial class C { }
";

            var expected = @"
internal partial class C { }
";

            Verify(text, expected, runFormatter: false);
        }


        [Fact]
        public void CorrectPartialType()
        {
            var text = @"
partial class C { }
public partial class C { }
";

            var expected = @"
public partial class C { }
public partial class C { }
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void PartialAcrossFiles()
        {
            var text1 = @"
public partial class C { }
";

            var text2 = @"
partial class C { }
";

            var expected1 = @"
public partial class C { }
";

            var expected2 = @"
public partial class C { }
";

            Verify(new[] { text1, text2 }, new[] { expected1, expected2 }, runFormatter: false);
        }

        [Fact]
        public void PartialTypesWithNestedClasses()
        {
            var text = @"
partial class C {
    class N1 { }
    class N2 { }
}

public partial class C { }
";

            var expected = @"
public partial class C {
    private class N1 { }
    private class N2 { }
}

public partial class C { }
";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void IgnorePartialMethods()
        {
            var text = @"
class C
{
    void M1();
    partial void M2();
}
";

            var expected = @"
internal class C
{
    private void M1();
    partial void M2();
}
";

            Verify(text, expected, runFormatter: false);
        }
    }
}
