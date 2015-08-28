// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(IOptionsProvider)), Shared]
    internal class UnwrittenWritableFieldOptions : IOptionsProvider
    {
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                Enabled
            }.ToImmutableArray();
        }

        private const string AnalyzerName = AnalyzerIds.UnwrittenWritableField + "." + "UnwrittenWritableField";

        /// <summary>
        /// Enable introduction of 'readonly' qualifier on fields that are only initialized and not subsequently modified.
        /// </summary>
        public static PerLanguageOption<bool> Enabled { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(Enabled), defaultValue: true);
    }
}