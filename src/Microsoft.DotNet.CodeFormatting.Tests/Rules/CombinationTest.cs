// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using System.Collections.Immutable;
using Microsoft.DotNet.CodeFormatting.Rules;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    /// <summary>
    /// A test which runs all rules on a given piece of code 
    /// </summary>
    public sealed class CombinationTest : CodeFormattingTestBase
    {
        private FormattingEngineImplementation _formattingEngine;

        public CombinationTest()
        {
            _formattingEngine = (FormattingEngineImplementation)FormattingEngine.Create();
            _formattingEngine.CopyrightHeader = ImmutableArray.Create("// header");
            _formattingEngine.AllowTables = true;
            _formattingEngine.FormatLogger = new EmptyFormatLogger();
            _formattingEngine.PreprocessorConfigurations = ImmutableArray<string[]>.Empty;
        }

        private void DisableAllRules()
        {
            foreach (var rule in _formattingEngine.AllRules)
            {
                _formattingEngine.ToggleRuleEnabled(rule, enabled: false);
            }
        }

        private void ToggleRule(string name, bool enabled)
        {
            var rule = _formattingEngine.AllRules.Where(x => x.Name == name).Single();
            _formattingEngine.ToggleRuleEnabled(rule, enabled);
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var solution = await _formattingEngine.FormatCoreAsync(
                document.Project.Solution,
                new[] { document.Id },
                CancellationToken.None);
            return solution.GetDocument(document.Id);
        }

        [Fact]
        public void FieldUse()
        {
            var text = @"
class C {
    int field;

    void M() {
        N(this.field);
    }
}";

            var expected = @"
internal class C
{
    private int _field;

    private void M()
    {
        N(_field);
    }
}";

            Verify(text, expected, runFormatter: false);
        }

        /// <summary>
        /// Ensure the engine respects the rule map
        /// </summary>
        [Fact]
        public void FieldOnly()
        {
            var text = @"
class C {
    int field;

    void M() {
        N(this.field);
    }
}";

            var expected = @"
class C {
    int _field;

    void M() {
        N(this._field);
    }
}";

            DisableAllRules();
            ToggleRule(PrivateFieldNamingRule.Name, enabled: true);

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void FieldNameExcluded()
        {
            var text = @"
class C {
    int field;

    void M() {
        N(this.field);
    }
}";

            var expected = @"
internal class C
{
    private int field;

    private void M()
    {
        N(field);
    }
}";

            ToggleRule(PrivateFieldNamingRule.Name, enabled: false);
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void FieldAssignment()
        {
            var text = @"
class C {
    int field;

    void M() {
        this.field = 42;
    }
}";

            var expected = @"
internal class C
{
    private int _field;

    private void M()
    {
        _field = 42;
    }
}";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void PreprocessorSymbolNotDefined()
        {
            var text = @"
class C
{
#if DOG
    void M() { } 
#endif 
}";

            var expected = @"
internal class C
{
#if DOG
    void M() { } 
#endif 
}";

            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void PreprocessorSymbolDefined()
        {
            var text = @"
internal class C
{
#if DOG
    internal void M() {
} 
#endif 
}";

            var expected = @"
internal class C
{
#if DOG
    internal void M()
    {
    }
#endif
}";

            _formattingEngine.PreprocessorConfigurations = ImmutableArray.CreateRange(new[] { new[] { "DOG" } });
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TableCode()
        {
            var text = @"
class C
{
    void G() { 

    }

#if !DOTNET_FORMATTER
    void M() {
}
#endif 
}";

            var expected = @"
internal class C
{
    private void G()
    {
    }

#if !DOTNET_FORMATTER
    void M() {
}
#endif 
}";

            Verify(text, expected, runFormatter: false);
        }

        /// <summary>
        /// Make sure the code which deals with additional configurations respects the
        /// table exception.
        /// </summary>
        [Fact]
        public void TableCodeAndAdditionalConfiguration()
        {
            var text = @"
class C
{
#if TEST
    void G(){
    }
#endif

#if !DOTNET_FORMATTER
    void M() {
}
#endif 
}";

            var expected = @"
internal class C
{
#if TEST
    void G()
    {
    }
#endif

#if !DOTNET_FORMATTER
    void M() {
}
#endif 
}";

            _formattingEngine.PreprocessorConfigurations = ImmutableArray.CreateRange(new[] { new[] { "TEST" } });
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void WhenBlocks()
        {
            var source = @"
internal class C
{
    private void M()
    {
        try
        {
            if(x){
                G();
            }
        } catch(Exception e)when(H(e))
        {

        }
    }
}";

            var expected = @"
internal class C
{
    private void M()
    {
        try
        {
            if (x)
            {
                G();
            }
        }
        catch (Exception e) when (H(e))
        {
        }
    }
}";

            Verify(source, expected, runFormatter: false);
        }

        [Fact]
        public void CSharpHeaderCorrectAfterMovingUsings()
        {

            var source = @"
namespace Microsoft.Build.UnitTests
{
    using System;
    using System.Reflection;
 
    public class Test
    {
        public void RequiredRuntimeAttribute() 
       {}
    }
}";
            var expected = @"
using System;
using System.Reflection;

namespace Microsoft.Build.UnitTests
{
    public class Test
    {
        public void RequiredRuntimeAttribute()
        { }
    }
}";

            // Using location rule is off by default.
            ToggleRule(UsingLocationRule.Name, enabled: true);
            Verify(source, expected);
        }

        [Fact]
        public void Issue268()
        {
            var text = @"
using System.Collections.Generic;

internal class C
{
    private void M<TValue>()
    {
        Dictionary<string, Stack<TValue>> dict = new Dictionary<string, Stack<TValue>>();
        dict.TryGetValue(""key"", out Stack<TValue> stack);
    }
}";

            Verify(text, expected:text);
        }

        [Fact]
        public void Issue272()
        {
            var text = @"
using System.Collections.Generic;

internal class C
{

        private object myVariable;

        private void M()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>()
            {
                { ""key"", new object() }
            };

            dict.TryGetValue(""key"", out object myVariable);

            this.myVariable = myVariable;
        }
}";
            var expected = @"
using System.Collections.Generic;

internal class C
{
    private object _myVariable;

    private void M()
    {
        Dictionary<string, object> dict = new Dictionary<string, object>()
            {
                { ""key"", new object() }
            };

        dict.TryGetValue(""key"", out object myVariable);

        _myVariable = myVariable;
    }
}";

            Verify(text, expected);
        }
    }
}
