// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.CodeAnalysis.Options;
using Xunit;

namespace Microsoft.DotNet.CodeFormatter.Analyzers.Tests
{
    public sealed class ExplicitThisAnalyzerTests : AnalyzerFixerTestBase
    {
        public ExplicitThisAnalyzerTests()
        {
            OptionsHelper.GetPropertiesImplementation = (analyzerOptions) =>
            {
                PropertyBag properties = CreatePolicyThatDisablesAllAnalysis();
                properties.SetProperty(OptionsHelper.BuildDefaultEnabledProperty(ExplicitThisAnalyzer.AnalyzerName), true);
                return properties;
            };
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

        public const string TestFieldAssignmentWithTrivia_Expected = @"class C1
{
    int _field;

    void M()
    {
        /* comment1 */
        _field /* comment 2 */ = 0;
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

        [Fact]
        public void TestFieldBadName()
        {
            Verify(TestFieldBadName_Input, TestFieldBadName_Expected, runFormatter: false);
        }
    }
}
