// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class HasNoUnusedUsingsRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.HasNoUnusedUsingsRule(); }
        }

        [Fact]
        public void SimpleMove()
        {
            var source = @"
using System;
namespace NS1
{
    class C1 { }
}";

            var expected = @"
namespace NS1
{
    class C1 { }
}";

            Verify(source, expected);
        }

        [Fact]
        public void RemoveUnusedUsingWithLeadingComment()
        {
            var source = @"
// copyright

using System;
namespace NS2
{
    class C1 { }
}";

            var expected = @"
// copyright

namespace NS2
{
    class C1 { }
}";

            Verify(source, expected);
        }

        [Fact]
        public void RemoveUnusedUsingsWithBeforeAfterComments()
        {
            var source = @"
// before
using System;
// after
namespace NS2
{
    class C1 { }
}";

            var expected = @"
// before
// after
namespace NS2
{
    class C1 { }
}";

            Verify(source, expected);
        }

        /// <summary>
        /// In the case a using directive is inside of a #if directive there is no
        /// way to safely remove the using.
        /// </summary>
        [Fact]
        public void KeepUnusedUsingsInDirectives()
        {
            var source = @"
#if false
using System;
#else
using System;
#endif

namespace N
{
    class C { }
}";

            Verify(source, source);
        }

        [Fact]
        public void KeepUsedUsingDirective()
        {
            var source = @"
using System;

namespace N
{
    class C
    {
        public void Method()
        {
            Console.WriteLine(""Hello, world!"");
        }
    }
}
";
            Verify(source, source);
        }

        [Fact]
        public void KeepSomeUsingDirectives()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace N
{
    class C
    {
        public void Method()
        {
            Console.WriteLine(new List<string>());
        }
    }
}
";
            var expected = @"
using System;
using System.Collections.Generic;

namespace N
{
    class C
    {
        public void Method()
        {
            Console.WriteLine(new List<string>());
        }
    }
}
";
            Verify(source, expected);
        }
    }
}
