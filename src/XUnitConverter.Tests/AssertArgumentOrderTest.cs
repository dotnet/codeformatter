// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using Xunit;

namespace XUnitConverter.Tests
{
    public class AssertArgumentOrderTest : ConverterTestBase
    {
        protected override XUnitConverter.ConverterBase CreateConverter()
        {
            return new XUnitConverter.AssertArgumentOrderConverter();
        }

        [Fact]
        public async Task TestSwapInvertedEqual()
        {
            string source = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Xunit.Assert.Equal(actual, 1);
    }
}
";
            string expected = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Xunit.Assert.Equal(1, actual);
    }
}
";

            await Verify(source, expected);
        }

        [Fact]
        public async Task TestSwapInvertedEqualEnum()
        {
            string source = @"
public class Tests
{
    private enum E
    {
        A,
        B,
    }

    public void TestA()
    {
        E actual = E.A;
        Xunit.Assert.Equal(actual, E.A);
    }
}
";
            string expected = @"
public class Tests
{
    private enum E
    {
        A,
        B,
    }

    public void TestA()
    {
        E actual = E.A;
        Xunit.Assert.Equal(E.A, actual);
    }
}
";
            await Verify(source, expected);
        }

        [Fact]
        public async Task TestSwapInvertedEqualConstField()
        {
            string source = @"
public class Tests
{
    private const int A;

    public void TestA()
    {
        int actual = A;
        Xunit.Assert.Equal(actual, A);
    }
}
";
            string expected = @"
public class Tests
{
    private const int A;

    public void TestA()
    {
        int actual = A;
        Xunit.Assert.Equal(A, actual);
    }
}
";
            await Verify(source, expected);
        }

        [Fact]
        public async Task TestSwapInvertedNotEqual()
        {
            string source = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Xunit.Assert.NotEqual(actual, 1);
    }
}
";
            string expected = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Xunit.Assert.NotEqual(1, actual);
    }
}
";
            await Verify(source, expected);
        }

        [Fact]
        public async Task TestSwapInvertedEqualFromUsing()
        {
            string source = @"
using Xunit;

public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Assert.Equal(actual, 1);
    }
}
";
            string expected = @"
using Xunit;

public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Assert.Equal(1, actual);
    }
}
";
            await Verify(source, expected);
        }

        [Fact]
        public async Task TestIgnoredCorrectEqual()
        {
            string text = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Xunit.Assert.Equal(1, actual);
    }
}
";
            await Verify(text, text);
        }

        [Fact]
        public async Task TestIgnoredDoubleConstEqual()
        {
            string text = @"
public class Tests
{
    public void TestA()
    {
        Xunit.Assert.Equal(1, 2);
    }
}
";
            await Verify(text, text);
        }

        [Fact]
        public async Task TestIgnoredDoubleVariableEqual()
        {
            string text = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        int expected = 1;
        Xunit.Assert.Equal(actual, expected);
    }
}
";
            await Verify(text, text);
        }

        [Fact]
        public async Task TestIgnoredCorrectNotEqual()
        {
            string text = @"
public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Xunit.Assert.NotEqual(1, actual);
    }
}
";
            await Verify(text, text);
        }

        [Fact]
        public async Task TestIgnoreOtherAssert()
        {
            string text = @"
public class Assert
{
    public void Equal(int expected, int actual)
    {
    }
}

public class Tests
{
    public void TestA()
    {
        int actual = 1;
        Assert.NotEqual(1, actual);
    }
}
";
            await Verify(text, text);
        }
    }
}
