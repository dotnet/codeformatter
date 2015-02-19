// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class PrivateFieldNamingRuleTests : GlobalSemanticRuleTestBase
    {
        internal override IGlobalSemanticFormattingRule Rule
        {
            get { return new Rules.PrivateFieldNamingRule(); }
        }

        private sealed class CSharpFields : PrivateFieldNamingRuleTests
        {
            [Fact]
            public void TestUnderScoreInPrivateFields()
            {
                var text = @"
using System;
class T
{
    private static int x;
    private static int s_y;
    // some trivia
    private static int m_z;
    // some trivia
    private int k = 1, m_s = 2, rsk_yz = 3, x_y_z;
    // some trivia
    [ThreadStatic] static int r;
    [ThreadStaticAttribute] static int b_r;
}";
                var expected = @"
using System;
class T
{
    private static int s_x;
    private static int s_y;
    // some trivia
    private static int s_z;
    // some trivia
    private int _k = 1, _s = 2, _rsk_yz = 3, _y_z;
    // some trivia
    [ThreadStatic]
    static int t_r;
    [ThreadStaticAttribute]
    static int t_r;
}";
                Verify(text, expected);
            }

            [Fact]
            public void CornerCaseNames()
            {
                var text = @"
class C
{
    private int x_;
    private int _;
    private int __;
    private int m_field1;
    private int field2_;
";

                var expected = @"
class C
{
    private int _x;
    private int _;
    private int __;
    private int _field1;
    private int _field2;
";

                Verify(text, expected, runFormatter: false);
            }

            [Fact]
            public void MultipleDeclarators()
            {
                var text = @"
class C1
{
    private int field1, field2, field3;
}

class C2
{
    private static int field1, field2, field3;
}

class C3
{
    internal int field1, field2, field3;
}
";

                var expected = @"
class C1
{
    private int _field1, _field2, _field3;
}

class C2
{
    private static int s_field1, s_field2, s_field3;
}

class C3
{
    internal int field1, field2, field3;
}
";

                Verify(text, expected, runFormatter: true);
            }

            /// <summary>
            /// If the name is pascal cased make it camel cased during the rewrite.  If it is not
            /// pascal cased then do not change the casing.
            /// </summary>
            [Fact]
            public void NameCasingField()
            {
                var text = @"
class C
{
    int Field;
    static int Other;
    int GCField;
    static int GCOther;
}
";

                var expected = @"
class C
{
    int _field;
    static int s_other;
    int _GCField;
    static int s_GCOther;
}
";

                Verify(text, expected, runFormatter: false);
            }

            [Fact]
            public void Issue68()
            {
                var text = @"
delegate void Action();
class C
{
    Action someAction;
    void M(C p)
    {
        someAction();
        this.someAction();
        p.someAction();
    }
}";

                var expected = @"
delegate void Action();
class C
{
    Action _someAction;
    void M(C p)
    {
        _someAction();
        this._someAction();
        p._someAction();
    }
}";

                Verify(text, expected);
            }

            /// <summary>
            /// Ensure that Roslyn properly renames private fields when accessed through a non-this
            /// instance within the same type.
            /// </summary>
            [Fact]
            public void Issue69()
            {
                var text = @"
class C
{
    int field;

    int M(C p)
    {
        int x = p.field;
        return x;
    }
}";

                var expected = @"
class C
{
    int _field;

    int M(C p)
    {
        int x = p._field;
        return x;
    }
}";

                Verify(text, expected);
            }
        }

        private sealed class VisualBasicFields : PrivateFieldNamingRuleTests
        {
            [Fact]
            public void Simple()
            {
                var text = @"
Class C 
    Private Field As Integer
End Class";

                var expected = @"
Class C 
    Private _field As Integer
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void ModuleFieldsAreShared()
            {
                var text = @"
Module C
    Private Field As Integer
End Module";

                var expected = @"
Module C
    Private s_field As Integer
End Module";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void MultipleDeclarations()
            {
                var text = @"
Class C 
    Private Field1, Field2 As Integer
End Class";

                var expected = @"
Class C 
    Private _field1,_field2 As Integer
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void FieldAndUse()
            {
                var text = @"
Class C 
    Private Field As Integer

    Sub M()
        Console.WriteLine(Field)
    End Sub
End Class";

                var expected = @"
Class C 
    Private _field As Integer

    Sub M()
        Console.WriteLine(_field)
    End Sub
End Class";

                Verify(text, expected, runFormatter: false, languageName: LanguageNames.VisualBasic);
            }

            [Fact]
            public void Issue69()
            {
                var text = @"
Class C1
    Private Field As Integer

    Function M(p As C1) As Integer
        Dim x = p.Field
        Return x
    End Function
End Class";

                var expected = @"
Class C1
    Private _field As Integer

    Function M(p As C1) As Integer
        Dim x = p._field
        Return x
    End Function
End Class";

                Verify(text, expected, languageName: LanguageNames.VisualBasic);
            }
        }
    }
}
