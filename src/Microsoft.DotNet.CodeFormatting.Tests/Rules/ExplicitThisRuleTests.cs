// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitThisRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.ExplicitThisRule(); }
        }

        [Fact]
        public void TestFieldUse()
        {
            Verify(ExplicitThisAnalyzerTests.TestFieldUse_Input, ExplicitThisAnalyzerTests.TestFieldUse_Expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldAssignment()
        {
            Verify(ExplicitThisAnalyzerTests.TestFieldAssignment_Input, ExplicitThisAnalyzerTests.TestFieldAssignment_Expected, runFormatter: false);
        }

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
            Verify(ExplicitThisAnalyzerTests.TestFieldAssignmentWithTrivia_Input, TestFieldAssignmentWithTrivia_Expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldBadName()
        {
            Verify(ExplicitThisAnalyzerTests.TestFieldBadName_Input, ExplicitThisAnalyzerTests.TestFieldBadName_Expected, runFormatter: false);
        }
    }
}
