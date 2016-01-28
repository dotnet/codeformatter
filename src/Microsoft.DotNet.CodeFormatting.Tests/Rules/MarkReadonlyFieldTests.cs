// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.CodeAnalysis;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    /// <summary>
    /// Test that fields are correctly identified as readonly.
    /// </summary>
    public class MarkReadonlyFieldTests : GlobalSemanticRuleTestBase
    {
        internal override IGlobalSemanticFormattingRule Rule
        {
            get { return new Rules.MarkReadonlyFieldsRule(); }
        }

        protected override IEnumerable<MetadataReference> GetSolutionMetadataReferences()
        {
            foreach (MetadataReference reference in base.GetSolutionMetadataReferences())
            {
                yield return reference;
            }

            yield return MetadataReference.CreateFromFile(typeof(ImportAttribute).Assembly.Location);
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

        [Fact]
        public void TestMarkReadonlyWithNoReferences()
        {
            string text = @"
class C
{
    private READONLY int read;
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestMarkReadonlyInternalWithNoReferences()
        {
            string text = @"
class C
{
    internal READONLY int read;
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyInternalWithNoReferencesByInternalsVisibleTo()
        {
            string text = @"
[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute(""Some.Other.Assembly"")]
class C
{
    internal int exposed;
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredPublic()
        {
            string text = @"
public class C
{
    public int exposed;
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestMarkedPublicInInternalClass()
        {
            string text = @"
internal class C
{
    public READONLY int notExposed;
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyWithWriteReferences()
        {
            string text = @"
class C
{
    private int wrote;

    public void T()
    {
        wrote = 5;
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyWithCompoundWriteReferences()
        {
            string text = @"
class C
{
    private int wrote;

    public void T()
    {
        wrote += 5;
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestMarkReadonlyWithReadReferences()
        {
            string text = @"
class C
{
    private READONLY int read;
    private int writen;

    public void T()
    {
        int x = change;
        x = read;
        writen = read;
        X(read);
    }

    public void X(int a)
    {
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyWithRefArgument()
        {
            string text = @"
class C
{
    private int read;

    public void M(ref int a)
    {
    }

    public void T()
    {
        M(ref read);
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyWithOutArgument()
        {
            string text = @"
class C
{
    private int read;

    public void N(out int a)
    {
    }

    public void T()
    {
        N(out read);
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyWithExternRefArgument()
        {
            string text = @"
class C
{
    private int read;

    private extern void M(ref C c);
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredReadonlyWithMethodCall()
        {
            string text = @"
struct S
{
    public void T() { }
}

class C
{
    private S called;

    public void T()
    {
        called.T();
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestMarkReadonlyWithPrimitiveMethodCall()
        {
            string text = @"

class C
{
    private READONLY int called;

    public void T()
    {
        string s = called.ToString();
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoredImportedField()
        {
            string text = @"
using System.ComponentModel.Composition;

public interface ITest
{
}

[Export(typeof(ITest))]
public class Test : ITest
{
}

class C
{
    [Import]
    private ITest imported;
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestMarkReadonlyWithWriteReferencesInConstructor()
        {
            string text = @"
class C
{
    private READONLY int read;

    public C()
    {
        read = 5;
        M(ref read);
        N(out read);
    }

    public void M(ref int a)
    {
    }

    public void N(out int a)
    {
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoreReadonlyWithDelegateReferencesInConstructor()
        {
            string text = @"
class C
{
    private int wrote;

    public C()
    {
        Action a = delegate { wrote = 5 };
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestIgnoreStaticReadonlyWithWriteReferencesInInstanceConstructor()
        {
            string text = @"
class C
{
    private static int wrote;

    public C()
    {
        wrote = 5;
    }
}
";
            Verify(Original(text), Readonly(text));
        }

        [Fact]
        public void TestMultipleFiles()
        {
            string[] text =
            {
                @"
class C1
{
    internal READONLY int read;
    internal int wrote;

    public void M(C2 c)
    {
        c.wrote = 5;
        int x = c.read;
    }
}
",
                @"
class C2
{
    internal READONLY int read;
    internal int wrote;

    public void M(C1 c)
    {
        c.wrote = 5;
        int x = c.read;
    }
}
"
            };
            Verify(Original(text), Readonly(text), true, LanguageNames.CSharp);
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
