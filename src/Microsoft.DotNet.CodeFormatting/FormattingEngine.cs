// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition.Convention;
using System.Composition.Hosting;

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

        public static List<IRuleMetadata> GetFormattingRules()
        {
            var container = CreateCompositionContainer();
            var list = new List<IRuleMetadata>();
            AppendRules<ISyntaxFormattingRule>(list, container);
            AppendRules<ILocalSemanticFormattingRule>(list, container);
            AppendRules<IGlobalSemanticFormattingRule>(list, container);
            return list;
        }

        private static void AppendRules<T>(List<IRuleMetadata> list, CompositionHost container)
            where T : IFormattingRule
        {
            foreach (var rule in container.GetExports<T>())
            {
                //list.Add(rule.Metadata);
            }
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
            conventions.ForTypesDerivedFrom<ISyntaxFormattingRule>()
                .Export<ISyntaxFormattingRule>();
            conventions.ForTypesDerivedFrom<ILocalSemanticFormattingRule>()
                .Export<ILocalSemanticFormattingRule>();
            conventions.ForTypesDerivedFrom<IGlobalSemanticFormattingRule>()
                .Export<IGlobalSemanticFormattingRule>();

            return conventions;
        }
    }
}