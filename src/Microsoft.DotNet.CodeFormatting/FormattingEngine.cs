// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using Microsoft.DotNet.CodeFormatting.Rules;
using Microsoft.DotNet.CodeFormatting.Filters;
using System.Collections.Immutable;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        public static IFormattingEngine Create()
        {
            var container = CreateCompositionContainer();
            var engine = container.GetExportedValue<IFormattingEngine>();
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

        private static void AppendRules<T>(List<IRuleMetadata> list, CompositionContainer container)
            where T : IFormattingRule
        {
            foreach (var rule in container.GetExports<T, IRuleMetadata>())
            {
                list.Add(rule.Metadata);
            }
        }

        private static CompositionContainer CreateCompositionContainer()
        {
            var catalog = new AssemblyCatalog(typeof(FormattingEngine).Assembly);
            return new CompositionContainer(catalog);
        }
    }
}
