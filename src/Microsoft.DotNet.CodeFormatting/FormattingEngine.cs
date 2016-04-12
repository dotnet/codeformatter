// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis.Options;

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

        public static ImmutableArray<IOptionsProvider> GetOptionsProviders(IEnumerable<Assembly> assemblies)
        {
            var container = CreateCompositionContainer(assemblies);
            return container.GetExports<IOptionsProvider>().ToImmutableArray();
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
            // New per-analyzer options mechanism, deriving
            // from VS Workspaces functionality 
            conventions.ForTypesDerivedFrom<IOptionsProvider>()
                                        .Export<IOptionsProvider>();

            // Legacy CodeFormatter rules options mechanism
            conventions.ForType<FormattingOptions>()
                .Export();

            conventions.ForTypesDerivedFrom<IFormattingEngine>()
                .Export<IFormattingEngine>();

            return conventions;
        }
    }
}
