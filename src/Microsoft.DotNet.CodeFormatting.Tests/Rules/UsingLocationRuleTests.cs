// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class UsingLocationRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.UsingLocationRule(); }
        }

        [Fact]
        public void SimpleMove()
        {
            var source = @"
using NS1;
namespace NS2
{
    using NS3;
    class C1 { }
}";

            var expected = @"
using NS1;
using NS3;
namespace NS2
{
    class C1 { }
}";

            Verify(source, expected);
        }

        /// <summary>
        /// There is no safe way to move a using outside a namespace when there are
        /// multiple namespaces.  The rule should punt on this scenario. 
        /// </summary>
        [Fact]
        public void SimpleMoveMultipleNamespaces()
        {
            var source = @"
using NS1;
namespace NS2
{
    using NS3;
    class C1 { }
}

namespace NS3
{
    using NS4;
    class C1 { }
}";

            Verify(source, source);
        }

        [Fact]
        public void SimpleMoveWithComment()
        {
            var source = @"
using NS1;
namespace NS2
{
    // test
    using NS3;
    class C1 { }
}";

            var expected = @"
using NS1;
// test
using NS3;
namespace NS2
{
    class C1 { }
}";

            Verify(source, expected);
        }

        [Fact]
        public void SimpleMoveWithBeforeAfterComments()
        {
            var source = @"
using NS1;
namespace NS2
{
    // test1
    using NS3;
    // test2
    class C1 { }
}";

            var expected = @"
using NS1;
// test1
using NS3;
namespace NS2
{
    // test2
    class C1 { }
}";

            Verify(source, expected);
        }

        [Fact]
        public void MoveToEmptyList()
        {
            var source = @"namespace NS2
{
    // test
    using NS3;
    class C1 { }
}";

            var expected = @"// test
using NS3;
namespace NS2
{
    class C1 { }
}";

            Verify(source, expected);
        }

        /// <summary>
        /// In the case a using directive is inside of a #pragma directive there is no
        /// way to safely move the using.
        /// </summary>
        [Fact]
        public void Issue71()
        {
            var source = @"
namespace N
{
#if false
    using NS1;
    class C { } 
#else
    using NS2;
    using D { } 
#endif
}";

            Verify(source, source);
        }
    }
}
