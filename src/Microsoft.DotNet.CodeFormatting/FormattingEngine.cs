// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        public static IFormattingEngine Create(IEnumerable<Assembly> assemblies = null)
        {
            var container = CreateCompositionContainer(assemblies);
            var engine = container.GetExport<IFormattingEngine>();
            var consoleFormatLogger = new ConsoleFormatLogger();
            return engine;
        }

        public static ImmutableArray<IRuleMetadata> GetFormattingRules(IEnumerable<Assembly> assemblies = null)
        {
            var container = CreateCompositionContainer(assemblies);
            var engine = container.GetExport<IFormattingEngine>();
            return engine.AllRules;
        }

        public static ImmutableArray<DiagnosticDescriptor> GetSupportedDiagnostics(IEnumerable<Assembly> assemblies)
        {
            var container = CreateCompositionContainer(assemblies);
            var engine = container.GetExport<IFormattingEngine>();
            return engine.AllSupportedDiagnostics;
        }

        private static CompositionHost CreateCompositionContainer(IEnumerable<Assembly> assemblies = null)
        {
            ConventionBuilder conventions = GetConventions();

            assemblies = assemblies ?? new Assembly[] { typeof(FormattingEngine).Assembly };

            return new ContainerConfiguration()
                .WithAssemblies(assemblies, conventions)
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