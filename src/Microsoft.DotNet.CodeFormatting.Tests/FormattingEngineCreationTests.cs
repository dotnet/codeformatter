using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using CodeFormatter;
using Microsoft.DotNet.CodeFormatter.Analyzers;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class FormattingEngineCreationTests
    {

        private static Assembly[] DefaultCompositionAssemblies =
                                        new Assembly[] {
                                            typeof(FormattingEngine).Assembly,
                                            typeof(OptimizeNamespaceImportsAnalyzer).Assembly
                                        };

        private static string TestDLLDir = Path.Combine(Directory.GetCurrentDirectory(), "TestAnalyzers");
        private static string RoslynV100Analyzer =  Path.Combine(TestDLLDir, "RoslynV100Analyzer.dll");
        private static string RoslynV110Analyzer = Path.Combine(TestDLLDir, "RoslynV110Analyzer.dll");
        private static string RoslynV111Analyzer = Path.Combine(TestDLLDir, "RoslynV111Analyzer.dll");
        private static string RoslynV120Beta1Analyzer = Path.Combine(TestDLLDir, "RoslynV120Beta1Analyzer.dll");
        private static string RoslynV120VBAnalyzer = Path.Combine(TestDLLDir, "RoslynV120VBAnalyzer.dll");

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV100()
        {
            IFormattingEngine engine = FormattingEngine.Create(DefaultCompositionAssemblies);
            Assert.DoesNotThrow(() => {
                var analyzers = Program.AddCustomAnalyzers(engine, ImmutableArray.Create(RoslynV100Analyzer));
                Assert.Equal(1, analyzers.Count());
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV110()
        {
            IFormattingEngine engine = FormattingEngine.Create(DefaultCompositionAssemblies);
            Assert.DoesNotThrow(() => {
                var analyzers = Program.AddCustomAnalyzers(engine, ImmutableArray.Create(RoslynV110Analyzer));
                Assert.Equal(1, analyzers.Count());
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV111()
        {
            IFormattingEngine engine = FormattingEngine.Create(DefaultCompositionAssemblies);
            Assert.DoesNotThrow(() => {
                var analyzers = Program.AddCustomAnalyzers(engine, ImmutableArray.Create(RoslynV111Analyzer));
                Assert.Equal(1, analyzers.Count());
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV120Beta1()
        {
            IFormattingEngine engine = FormattingEngine.Create(DefaultCompositionAssemblies);
            Assert.DoesNotThrow(() => {
                var analyzers = Program.AddCustomAnalyzers(engine, ImmutableArray.Create(RoslynV120Beta1Analyzer));
                Assert.Equal(1, analyzers.Count());
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV120()
        {
            IFormattingEngine engine = FormattingEngine.Create(DefaultCompositionAssemblies);
            Assert.DoesNotThrow(() => {
                var analyzers = Program.AddCustomAnalyzers(engine, ImmutableArray.Create(RoslynV120VBAnalyzer));
                Assert.Equal(1, analyzers.Count());
            });
        }
    }
}
