// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.DotNet.CodeFormatting.Rules;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class AttributeNoParenthesesRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new AttributeNoParenthesesRule(); }
        }

        [Fact]
        public void RemoveParenthesesFromAttributes()
        {
            var text = @"
[assembly: GlobalAtt()]
[assembly: GlobalAtt(1)]

namespace Namespace1
{
    [Serializable(), Category(2), Rule]
    class Class1
    {
        [return: SomeAtt(]
        [AnotherAtt(1), YetAnotherAtt(), YetAnotherAtt]
        public int SomeMethod(SyntaxNode syntaxRoot)
        {
            return 42;
        }
    }
}
";
            var expected = @"
[assembly: GlobalAtt]
[assembly: GlobalAtt(1)]

namespace Namespace1
{
    [Serializable, Category(2), Rule]
    class Class1
    {
        [return: SomeAtt]
        [AnotherAtt(1), YetAnotherAtt, YetAnotherAtt]
        public int SomeMethod(SyntaxNode syntaxRoot)
        {
            return 42;
        }
    }
}
";
            Verify(text, expected);
        }
    }
}