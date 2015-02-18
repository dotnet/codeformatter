// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace XUnitConverter.Tests
{
    public class TestAssertTrueOrFalseConverterTests : ConverterTestBase
    {
        protected override XUnitConverter.ConverterBase CreateConverter()
        {
            return new XUnitConverter.TestAssertTrueOrFalseConverter();
        }

        [Fact]
        public async Task TestAssertEqualNotEqual()
        {
            var text = @"
using System;
using Xunit;

namespace System.UnitTests
{
    public class Testing
    {
        [Fact]
        public void MyTest()
        {
            int x1 = 123, x2 = 456;
            Assert.True(x1 == x2);
            Assert.True(x2 != x1, ""Message"");

            Assert.False(x2 == x1, ""Message"");
            Assert.False(x1 != x2);
        }
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.UnitTests
{
    public class Testing
    {
        [Fact]
        public void MyTest()
        {
            int x1 = 123, x2 = 456;
            Assert.Equal(x1, x2);
            Assert.NotEqual(x2, x1);

            Assert.NotEqual(x2, x1);
            Assert.Equal(x1, x2);
        }
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestAssertEqualNotEqualNull()
        {
            var text = @"
using System;
using Xunit;

public class Testing
{
    [Fact]
    public void MyTest()
    {
        string s1 = ""A"", s2 = null;
        Assert.True(s1 == null);
        Assert.True(null != s2, ""Message"");

        Assert.False(null == s1, ""Message"");
        Assert.False(s2 != null);
    }
}
";

            var expected = @"
using System;
using Xunit;

public class Testing
{
    [Fact]
    public void MyTest()
    {
        string s1 = ""A"", s2 = null;
        Assert.Null(s1);
        Assert.NotNull(s2);

        Assert.NotNull(s1);
        Assert.Null(s2);
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestAssertEqualNotEqualTrueOrFalse()
        {
            var text = @"
using System;
using Xunit;

public class Testing
{
    [Fact]
    public void MyTest()
    {
        bool b1 = true, b2 = false;
        Assert.True(b1 == true);
        Assert.True(true != b2);
        Assert.True(b2 == false);
        Assert.True(false != b1);

        Assert.False(b1 == true);
        Assert.False(true != b2);
        Assert.False(b2 == false);
        Assert.False(false != b1);
    }
}
";

            var expected = @"
using System;
using Xunit;

public class Testing
{
    [Fact]
    public void MyTest()
    {
        bool b1 = true, b2 = false;
        Assert.True(b1);
        Assert.False(b2);
        Assert.False(b2);
        Assert.True(b1);

        Assert.False(b1);
        Assert.True(b2);
        Assert.True(b2);
        Assert.False(b1);
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestAssertEqualNotEqualNegation()
        {
            var text = @"
using System;
using Xunit;

public class Testing
{
    [Fact]
    public void MyTest()
    {
        bool b1 = true, b2 = false;
        Assert.True(!b1);
        Assert.False(!b2);
    }
}
";

            var expected = @"
using System;
using Xunit;

public class Testing
{
    [Fact]
    public void MyTest()
    {
        bool b1 = true, b2 = false;
        Assert.False(b1);
        Assert.True(b2);
    }
}
";
            await Verify(text, expected);
        }
    }
}
