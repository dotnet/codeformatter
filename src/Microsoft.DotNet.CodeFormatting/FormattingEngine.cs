// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.ComponentModel.Composition.Hosting;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        public static IFormattingEngine Create()
        {
            var catalog = new AssemblyCatalog(typeof(FormattingEngine).Assembly);
            var container = new CompositionContainer(catalog);
            var engine = container.GetExportedValue<IFormattingEngine>();
            return engine;
        }
    }
}