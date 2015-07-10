// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitThisAnalyzerTests : AnalyzerFixerTestBase
    {
        private static IFormattingEngine s_engine;

        static ExplicitThisAnalyzerTests()
        {
            s_engine = FormattingEngine.Create();
        }

        public ExplicitThisAnalyzerTests() : base(s_engine)
        {
        }

        [Fact]
        public void TestFieldUse()
        {
            Verify(nameof(TestFieldUse), runFormatter: false);
        }

        [Fact]
        public void TestFieldAssignment()
        {
            Verify(nameof(TestFieldAssignment), runFormatter: false);
        }

        [Fact]
        public void TestFieldAssignmentWithTrivia_AnalyzerBased()
        {
            Verify(nameof(TestFieldAssignmentWithTrivia_AnalyzerBased), runFormatter: false);
        }

        [Fact]
        public void TestFieldBadName()
        {
            Verify(nameof(TestFieldBadName), runFormatter: false);
        }
    }
}
