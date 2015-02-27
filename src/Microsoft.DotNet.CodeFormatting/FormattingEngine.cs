// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        public static IFormattingEngine Create(ImmutableArray<string> ruleTypes)
        {
            var catalog = new AssemblyCatalog(typeof(FormattingEngine).Assembly);
            var container = new CompositionContainer(catalog);
            var engine = container.GetExportedValue<IFormattingEngine>();
            var consoleFormatLogger = new ConsoleFormatLogger();
            return engine;
        }
    }
}