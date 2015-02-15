// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.DotNet.CodeFormatting.Rules;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class AttributeSeparateListsRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new AttributeSeparateListsRule(); }
        }

        [Fact]
        public void ParameterAttributeListsAreNotSeparated()
        {
            var text = @"
namespace Namespace1
{
    class Class1
    {
        public int SomeMethod([In, Out]SomeType someParameter)
        {
            return 42;
        }
    }
}";
            Verify(text, text);
        }

        [Fact]
        public void AttributeListsAreSeparated()
        {
            var text = @"
[assembly: FileVersion(1, 1), AssemblyVersion(1, 1)]
namespace Namespace1
{
    [
        Serializable,       // Good, isn't?
        DefaultValue(1)     // Is this the right value?
    ]
    class Class1
    {
        [Serializable, DefaultValue(1)]
        public int SomeMethod(SomeType someParameter)
        {
            return 42;
        }
    }
}";

            var expected = @"
[assembly: FileVersion(1, 1)]
[assembly: AssemblyVersion(1, 1)]
namespace Namespace1
{

    [Serializable]       // Good, isn't?
    [DefaultValue(1)]     // Is this the right value?

    class Class1
    {
        [Serializable]
        [DefaultValue(1)]
        public int SomeMethod(SomeType someParameter)
        {
            return 42;
        }
    }
}";
            Verify(text, expected);
        }
    }
}