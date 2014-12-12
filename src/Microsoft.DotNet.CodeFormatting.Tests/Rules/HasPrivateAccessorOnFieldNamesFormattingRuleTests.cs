// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.CodeFormatting;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class HasPrivateAccessorOnFieldNamesFormattingRuleTests : CodeFormattingTestBase
    {
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
class T
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

        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.HasPrivateAccessorOnFieldNamesFormattingRule();
        }
    }
}
