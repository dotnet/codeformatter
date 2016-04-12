using System;
using System.Collections.Generic;
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
        // TODO: fix hardcoded path
        private static string TestDLLDir = @"E:\src\codeformatter\src\Microsoft.DotNet.CodeFormatting.Tests\TestAnalyzers\";
        private static Assembly RoslynV100Analyzer =  Assembly.LoadFile(TestDLLDir + "RoslynV100Analyzer.dll");
        private static Assembly RoslynV110Analyzer = Assembly.LoadFile(TestDLLDir + "RoslynV110Analyzer.dll");
        private static Assembly RoslynV111Analyzer = Assembly.LoadFile(TestDLLDir + "RoslynV111Analyzer.dll");
        private static Assembly RoslynV120Beta1Analyzer = Assembly.LoadFile(TestDLLDir + "RoslynV120Beta1Analyzer.dll");

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV100()
        {
            IEnumerable<Assembly> roslynV1AnalyzerDLL = new Assembly[] { RoslynV100Analyzer };
            Assert.DoesNotThrow(() => {
                var assemblies = DefaultCompositionAssemblies.Concat(roslynV1AnalyzerDLL);
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV110()
        {
            IEnumerable<Assembly> roslynV110AnalyzerDLL = new Assembly[] { RoslynV110Analyzer };
            Assert.DoesNotThrow(() => {
                var assemblies = DefaultCompositionAssemblies.Concat(roslynV110AnalyzerDLL);
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV111()
        {
            IEnumerable<Assembly> roslynV111AnalyzerDLL = new Assembly[] { RoslynV111Analyzer };
            Assert.DoesNotThrow(() => {
                var assemblies = DefaultCompositionAssemblies.Concat(roslynV111AnalyzerDLL);
            });
        }

        [Fact]
        public void AnalyzersBuiltAgainstRoslynV120Beta1()
        {
            IEnumerable<Assembly> roslynV120Beta1AnalyzerDLL = new Assembly[] { RoslynV120Beta1Analyzer };
            Assert.DoesNotThrow(() => {
                var assemblies = DefaultCompositionAssemblies.Concat(roslynV120Beta1AnalyzerDLL);
            });
        }
    }
}
