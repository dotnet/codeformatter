// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class ExplicitVisibilityRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.ExplicitVisibilityRule(); }
        }

        public sealed class CSharpTests : ExplicitVisibilityRuleTests
        {

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

                Verify(new[] { text1, text2 }, new[] { expected1, expected2 }, runFormatter: false, languageName: LanguageNames.CSharp);
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

            [Fact]
            public void CommentAttributeAndType()
            {
                var text = @"
// Hello
[Attr]
// World
class C1 { }

// Hello
[Attr]
// World
partial class C2 { }";

                var expected = @"
// Hello
[Attr]
// World
internal class C1 { }

// Hello
[Attr]
// World
internal partial class C2 { }";

                Verify(text, expected);
            }

            [Fact]
            public void CommentAttributeAndMethod()
            {
                var text = @"
class C
{
    // Hello
    [Attr]
    // World
    void M1() { }

    // Hello
    [Attr]
    // World
    static void M2() { }
};";

                var expected = @"
internal class C
{
    // Hello
    [Attr]
    // World
    private void M1() { }

    // Hello
    [Attr]
    // World
    private static void M2() { }
};";

                Verify(text, expected);

            }

            [Fact]
            public void CommentAttributeAndMethod2()
            {
                var text = @"
class C
{
    // Hello
    [Attr]
    // World
    void M1() { }

    // Hello
    [Attr]
    // World
    override void M2() { }
};";

                var expected = @"
internal class C
{
    // Hello
    [Attr]
    // World
    private void M1() { }

    // Hello
    [Attr]
    // World
    private override void M2() { }
};";

                Verify(text, expected);

            }

            [Fact]
            public void CommentAttributeAndProperty()
            {
                var text = @"
class C
{
    // Hello
    [Attr]
    // World
    int P1 { get { return 0; } }

    // Hello
    [Attr]
    // World
    static int P2 { get { return 0; } }
};";

                var expected = @"
internal class C
{
    // Hello
    [Attr]
    // World
    private int P1 { get { return 0; } }

    // Hello
    [Attr]
    // World
    private static int P2 { get { return 0; } }
};";

                Verify(text, expected);

            }

            [Fact]
            public void CommentAttributeAndConstructor()
            {
                var text = @"
class C
{
    // Hello
    [Attr]
    // World
    C(int p1) { }

    // Hello
    [Attr]
    // World
    unsafe C(int p1, int p2) { }
};";

                var expected = @"
internal class C
{
    // Hello
    [Attr]
    // World
    private C(int p1) { }

    // Hello
    [Attr]
    // World
    private unsafe C(int p1, int p2) { }
};";

                Verify(text, expected);

            }

            public void CommentAttributeAndMultipleField()
            {
                var text = @"
class C 
{
    // Hello
    [Attr]
    // World
    int x, y;
};";

                var expected = @"
internal class C 
{
    // Hello
    [Attr]
    // World
    private int x, y;
};";

                Verify(text, expected);
            }

            [Fact]
            public void Issue70()
            {
                var source = @"
public class MyClass
{
    enum MyEnum { }
    struct MyStruct
    {
        public MyStruct(MyEnum e) { }
    }
}";

                var expected = @"
public class MyClass
{
    private enum MyEnum { }
    private struct MyStruct
    {
        public MyStruct(MyEnum e) { }
    }
}";

                Verify(source, expected);
            }
        }

        public sealed class VisualBasicTests : ExplicitVisibilityRuleTests
        {
            [Fact]
            public void TypeSimple()
            {
                var text = @"
Class C
End Class
Structure S
End Structure
Module M
End Module
Enum E
    Value
End Enum";

                var expected = @"
Friend Class C
End Class
Friend Structure S
End Structure
Friend Module M
End Module
Friend Enum E
    Value
End Enum";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void TypeWithCommentAndAttribute()
            {
                var text = @"
' Hello
<Attr>
Class C
End Class";

                var expected = @"
' Hello
<Attr>
Friend Class C
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void TypePartialWithCommentAndAttribute()
            {
                var text = @"
' Hello
<Attr>
Partial Class C
End Class";

                var expected = @"
' Hello
<Attr>
Friend Partial Class C
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            /// <summary>
            /// It is interesting to note that nested typs in VB.Net default to public accessibility 
            /// instead of private as C# does.
            /// </summary>
            [Fact]
            public void NestedType()
            {
                var text = @"
Class Outer
    Class C
    End Class
    Structure S
    End Structure
    Enum E
        Value
    End Enum
End Class";

                var expected = @"
Friend Class Outer
    Public Class C
    End Class
    Public Structure S
    End Structure
    Public Enum E
        Value
    End Enum
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void Methods()
            {
                var text = @"
Class C
    Sub M1()
    End Sub
    Function M2()
    End Function
    Private Sub M3()
    End Sub
End Class";

                var expected = @"
Friend Class C
    Public Sub M1()
    End Sub
    Public Function M2()
    End Function
    Private Sub M3()
    End Sub
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void MethodWithCommentAndAttribute()
            {
                var text = @"
Class C
    ' A Comment
    <Attr>
    Sub M1()
    End Sub
End Class";

                var expected = @"
Friend Class C
    ' A Comment
    <Attr>
    Public Sub M1()
    End Sub
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void Fields()
            {
                var text = @"
Class C
    Dim Field1 As Integer
    Public Field2 As Integer
End Class";

                var expected = @"
Friend Class C
    Private Field1 As Integer
    Public Field2 As Integer
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void Constructors()
            {
                var text = @"
Class C
    Sub New()
    End Sub
    Shared Sub New()
    End Sub
End Class";

                var expected = @"
Friend Class C
    Public Sub New()
    End Sub
    Shared Sub New()
    End Sub
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void ConstructorsWithCommentAndAttribute()
            {
                var text = @"
Class C
    ' Hello
    <Attr>
    Sub New()
    End Sub
    ' Hello
    <Attr>
    Shared Sub New()
    End Sub
End Class";

                var expected = @"
Friend Class C
    ' Hello
    <Attr>
    Public Sub New()
    End Sub
    ' Hello
    <Attr>
    Shared Sub New()
    End Sub
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            /// <summary>
            /// Members of a Module are implicitly Shared hence New here is a static ctor and cannot have
            /// any visibility modifiers.
            /// </summary>
            [Fact]
            public void ConstructorOnModules()
            {
                var text = @"
Module M
    Sub New()
    End Sub
End Module";

                var expected = @"
Friend Module M
    Sub New()
    End Sub
End Module";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void InterfaceMembers()
            {
                var text = @"
Interface I1
    Property P1 As Integer
    Sub S1()
    Function F1()
End Interface";

                var expected = @"
Friend Interface I1
    Property P1 As Integer
    Sub S1()
    Function F1()
End Interface";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            /// <summary>
            /// VB.Net can have visibility modifiers + explicit interface implementation unlike C#. The 
            /// visibility rules for these members is the same as normal members.
            /// </summary>
            [Fact]
            public void InterfaceImplementation()
            {
                var text = @"
Interface I1
    Property P1 As Integer
    Sub S1()
    Function F1()
End Interface

Class C1
    Implements I1

    Function F1() As Object Implements I1.F1
        Return Nothing
    End Function

    Property P1 As Integer Implements I1.P1

    Sub S1() Implements I1.S1
    End Sub
End Class";

                var expected = @"
Friend Interface I1
    Property P1 As Integer
    Sub S1()
    Function F1()
End Interface

Friend Class C1
    Implements I1

    Public Function F1() As Object Implements I1.F1
        Return Nothing
    End Function

    Public Property P1 As Integer Implements I1.P1

    Public Sub S1() Implements I1.S1
    End Sub
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }
        }
    }
}
