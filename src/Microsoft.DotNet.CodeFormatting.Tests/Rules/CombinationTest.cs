using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using System.Collections.Immutable;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    /// <summary>
    /// A test which runs all rules on a given piece of code 
    /// </summary>
    public sealed class CombinationTest : CodeFormattingTestBase, IDisposable
    {
        private static FormattingEngineImplementation s_formattingEngine;

        static CombinationTest()
        {
            s_formattingEngine = (FormattingEngineImplementation)FormattingEngine.Create(ImmutableArray<string>.Empty);
        }

        public CombinationTest()
        {
            s_formattingEngine.CopyrightHeader = ImmutableArray.Create("// header");
            s_formattingEngine.AllowTables = true;
            s_formattingEngine.FormatLogger = new EmptyFormatLogger();
            s_formattingEngine.PreprocessorConfigurations = ImmutableArray<string[]>.Empty;
        }

        public void Dispose()
        {
            s_formattingEngine.AllowTables = false;
        }

        protected override async Task<Document> RewriteDocumentAsync(Document document)
        {
            var solution = await s_formattingEngine.FormatCoreAsync(
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

            var expected = @"// header

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

            var expected = @"// header

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

            var expected = @"// header

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

            var expected = @"// header

internal class C
{
#if DOG
    internal void M()
    {
    }
#endif
}";

            s_formattingEngine.PreprocessorConfigurations = ImmutableArray.CreateRange(new[] { new[] { "DOG" } });
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

            var expected = @"// header

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

            var expected = @"// header

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

            s_formattingEngine.PreprocessorConfigurations = ImmutableArray.CreateRange(new[] { new[] { "TEST" } });
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

            var expected = @"// header

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
    }
}
