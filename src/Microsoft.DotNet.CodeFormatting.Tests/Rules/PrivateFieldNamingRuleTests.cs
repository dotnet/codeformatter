// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class PrivateFieldNamingRuleTests : CodeFormattingTestBase
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

        internal override IFormattingRule GetFormattingRule()
        {
            return new Rules.PrivateFieldNamingRule();
        }
    }
}
