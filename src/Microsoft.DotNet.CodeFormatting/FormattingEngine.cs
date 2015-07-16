// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        public static IFormattingEngine Create()
        {
            var container = CreateCompositionContainer();
            var engine = container.GetExport<IFormattingEngine>();
            var consoleFormatLogger = new ConsoleFormatLogger();
            return engine;
        }

        public static ImmutableArray<IRuleMetadata> GetFormattingRules()
        {
            var container = CreateCompositionContainer();
            var engine = container.GetExport<IFormattingEngine>();
            return engine.AllRules;
        }

        public static ImmutableArray<DiagnosticDescriptor> GetSupportedDiagnostics()
        {
            var container = CreateCompositionContainer();
            var engine = container.GetExport<IFormattingEngine>();
            return engine.AllSupportedDiagnostics;
        }

        private static CompositionHost CreateCompositionContainer()
        {
            ConventionBuilder conventions = GetConventions();

            return new ContainerConfiguration()
                .WithAssembly(typeof(FormattingEngine).Assembly, conventions)
                .CreateContainer();
        }

        private static ConventionBuilder GetConventions()
        {
            var conventions = new ConventionBuilder();

            conventions.ForTypesDerivedFrom<IFormattingFilter>()
                .Export<IFormattingFilter>();

            conventions.ForTypesDerivedFrom<ISyntaxFormattingRule>()
                .Export<ISyntaxFormattingRule>();
            conventions.ForTypesDerivedFrom<ILocalSemanticFormattingRule>()
                .Export<ILocalSemanticFormattingRule>();
            conventions.ForTypesDerivedFrom<IGlobalSemanticFormattingRule>()
                .Export<IGlobalSemanticFormattingRule>();

            conventions.ForType<Options>()
                .Export();

            conventions.ForTypesDerivedFrom<IFormattingEngine>()
                .Export<IFormattingEngine>();

            return conventions;
        }
    }
}