// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitThisRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.ExplicitThisRule(); }
        }

        public const string TestFieldUse_Input = @"class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(this._field1);
        Use(this._field2);
        Use(this.field3);
    }
}
";

        public const string TestFieldUse_Expected = @"class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(_field1);
        Use(_field2);
        Use(this.field3);
    }
}
";

        [Fact]
        public void TestFieldUse()
        {
            Verify(TestFieldUse_Input, TestFieldUse_Expected, runFormatter: false);
        }

        public const string TestFieldAssignment_Input = @"class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void M()
    {
        this._field1 = 0;
        this._field2 = null;
        this.field3 = null;
    }
}
";

        public const string TestFieldAssignment_Expected = @"class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void M()
    {
        _field1 = 0;
        _field2 = null;
        this.field3 = null;
    }
}
";

        [Fact]
        public void TestFieldAssignment()
        {
            Verify(TestFieldAssignment_Input, TestFieldAssignment_Expected, runFormatter: false);
        }

        public const string TestFieldAssignmentWithTrivia_Input = @"class C1
{
    int _field;

    void M()
    {
        this. /* comment1 */ _field /* comment 2 */ = 0;
        // before comment
        this._field = 42;
        // after comment
    }
}
";

        // The rule-based version behaves differently from the analyzer/fixer-based version
        // because the analyzer/fixer-based version always applies formatting -- at least
        // for now.
        public const string TestFieldAssignmentWithTrivia_Expected = @"class C1
{
    int _field;

    void M()
    {
         /* comment1 */ _field /* comment 2 */ = 0;
        // before comment
        _field = 42;
        // after comment
    }
}
";

        [Fact]
        public void TestFieldAssignmentWithTrivia()
        {
            Verify(TestFieldAssignmentWithTrivia_Input, TestFieldAssignmentWithTrivia_Expected, runFormatter: false);
        }

        public const string TestFieldBadName_Input = @"class C1
{
    int _field;

    void M()
    {
        // Not a valid field access, can't reliably remove this.
        this.field1 = 0;
    }
}
";

        public const string TestFieldBadName_Expected = @"class C1
{
    int _field;

    void M()
    {
        // Not a valid field access, can't reliably remove this.
        this.field1 = 0;
    }
}
";
    }
}
