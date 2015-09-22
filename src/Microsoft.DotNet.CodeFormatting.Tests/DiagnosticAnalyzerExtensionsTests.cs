// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public class DiagnosticAnalyzerExtensionsTests
    {
        [Export(typeof(DiagnosticAnalyzer))]
        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        private class CSharpAnalyzer : DiagnosticAnalyzer
        {
            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            {
                get
                {
                    return ImmutableArray<DiagnosticDescriptor>.Empty;
                }
            }

            public override void Initialize(AnalysisContext context)
            {
            }
        }

        [Fact]
        public void DiagnosticAnalyzer_SupportsLanguage_RecognizesCSharp()
        {
            var csharpAnalyzer = new CSharpAnalyzer();

            Assert.True(csharpAnalyzer.SupportsLanguage(LanguageNames.CSharp));
            Assert.False(csharpAnalyzer.SupportsLanguage(LanguageNames.VisualBasic));
        }

        [Export(typeof(DiagnosticAnalyzer))]
        [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
        private class VisualBasicAnalyzer : DiagnosticAnalyzer
        {
            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            {
                get
                {
                    return ImmutableArray<DiagnosticDescriptor>.Empty;
                }
            }

            public override void Initialize(AnalysisContext context)
            {
            }
        }

        [Fact]
        public void DiagnosticAnalyzer_SupportsLanguage_RecognizesVisualBasic()
        {
            var visualBasicAnalyzer = new VisualBasicAnalyzer();

            Assert.False(visualBasicAnalyzer.SupportsLanguage(LanguageNames.CSharp));
            Assert.True(visualBasicAnalyzer.SupportsLanguage(LanguageNames.VisualBasic));
        }

        [Export(typeof(DiagnosticAnalyzer))]
        [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
        private class MultipleLanguageAnalyzer : DiagnosticAnalyzer
        {
            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            {
                get
                {
                    return ImmutableArray<DiagnosticDescriptor>.Empty;
                }
            }

            public override void Initialize(AnalysisContext context)
            {
            }
        }

        [Fact]
        public void DiagnosticAnalyzer_SupportsLanguage_RecognizesMultipleLanguages()
        {
            var multipleLanguageAnalyzer = new MultipleLanguageAnalyzer();

            Assert.True(multipleLanguageAnalyzer.SupportsLanguage(LanguageNames.CSharp));
            Assert.True(multipleLanguageAnalyzer.SupportsLanguage(LanguageNames.VisualBasic));
        }
    }
}
