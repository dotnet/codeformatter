// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Threading;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class NewLineAboveRuleTests : SyntaxRuleTestBase
    {
        internal override ISyntaxFormattingRule Rule
        {
            get { return new Rules.NewLineAboveRule(); }
        }

        /// <summary>
        /// If there is already a new line before the using then no work should be done. 
        /// </summary>
        [Fact]
        public void NoWorkSimpleUsing()
        {
            var text = @"
// Comment

using System;";

            var syntaxTree = SyntaxFactory.ParseSyntaxTree(text);
            var root = syntaxTree.GetRoot(CancellationToken.None);
            var newRoot = Rule.Process(root, LanguageNames.CSharp);
            Assert.Same(root, newRoot);
        }

        /// <summary>
        /// If there is already a new line before the namespace then no work should be done. 
        /// </summary>
        [Fact]
        public void NoWorkSimpleNamespace()
        {
            var text = @"
// Comment

namespace NS1
{

}";
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(text);
            var root = syntaxTree.GetRoot(CancellationToken.None);
            var newRoot = Rule.Process(root, LanguageNames.CSharp);
            Assert.Same(root, newRoot);
        }

        [Fact]
        public void UsingWithOnlyDirectiveAbove()
        {
            var text = @"#pragma warning disable 1591
using System;";

            var expected = @"
#pragma warning disable 1591
using System;";

            Verify(text, expected);
        }

        [Fact]
        public void UsingWithOnlyBlankAbove()
        {
            var text = @"
using System;";

            Verify(text, text);
        }

        [Fact]
        public void StandardCombination()
        {
            var text = @"// copyright
using System;
using System.Collections;
namespace NS1
{

}";

            var expected = @"// copyright

using System;
using System.Collections;

namespace NS1
{

}";

            Verify(text, expected);
        }

        [Fact]
        public void StandardCombinationNoWork()
        {
            var text = @"// copyright

using System;
using System.Collections;

namespace NS1
{

}";

            Verify(text, text);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing01()
        {
            var text = @"
//some comment
using System;
";
            var expected = @"
//some comment

using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing02()
        {
            var text = @"
/*some comment1
some comment2
*/
using System;
";
            var expected = @"
/*some comment1
some comment2
*/

using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstUsing03()
        {
            var text = @"
// copyright comment
#pragma warning disable 1591
using System;
";
            var expected = @"
// copyright comment

#pragma warning disable 1591
using System;
";
            Verify(text, expected);
        }

        [Fact]
        public void ConditionalDirectivesNotSupported()
        {
            var text = @"
// some comment
#pragma warning disable 1561
#if true
using System;
#endif
";
            Verify(text, text);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace01()
        {
            var text = @"
using System;
//some comment
namespace N { }
";
            var expected = @"
using System;
//some comment

namespace N { }
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace02()
        {
            var text = @"
using System;
/*some comment1
some comment2
*/
namespace N { }
";
            var expected = @"
using System;
/*some comment1
some comment2
*/

namespace N { }
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace03()
        {
            var text = @"
using System;
#pragma warning disable 1591
namespace N { }
";
            var expected = @"
using System;

#pragma warning disable 1591
namespace N { }
";
            Verify(text, expected);
        }

        [Fact]
        public void TestNewLineBeforeFirstNamespace04()
        {
            var text = @"
// some comment
#pragma warning disable 1561
#if true
#endif
namespace N { }
";
            Verify(text, text);
        }
    }
}
