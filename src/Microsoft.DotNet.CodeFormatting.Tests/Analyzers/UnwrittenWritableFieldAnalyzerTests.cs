// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class UnwrittenWritableFieldAnalyzerTests : AnalyzerFixerTestBase
    {
        private static IFormattingEngine s_engine;

        static UnwrittenWritableFieldAnalyzerTests()
        {
            s_engine = FormattingEngine.Create();
        }

        public UnwrittenWritableFieldAnalyzerTests() : base(s_engine)
        {
        }

        // In general a single sting with "READONLY" in it is used
        // for the tests to simplify the before/after comparison
        // The Original method will remove it, and the Readonly will replace it
        // with the keyword

        [Fact]
        public void TestIgnoreExistingReadonlyField()
        {
            string text = @"
class C
{
    private readonly int alreadyFine;
}
";
            Verify(Original(text), Readonly(text));
        }

        private static string Original(string text)
        {
            return text.Replace("READONLY ", "");
        }

        private static string Readonly(string text)
        {
            return text.Replace("READONLY ", "readonly ");
        }

        private static string[] Original(string[] text)
        {
            return text.Select(Original).ToArray();
        }

        private static string[] Readonly(string[] text)
        {
            return text.Select(Readonly).ToArray();
        }
    }
}
