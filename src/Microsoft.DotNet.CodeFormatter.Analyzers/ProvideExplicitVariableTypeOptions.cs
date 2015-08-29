// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(IOptionsProvider)), Shared]
    internal class ProvideExplicitVariableTypeOptions : IOptionsProvider
    {
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                Enabled
            }.ToImmutableArray();
        }

        private const string AnalyzerName = AnalyzerIds.ProvideExplicitVariableType + "." + "ProvideExplicitVariable";

        /// <summary>
        /// Enable insertion of explicit variable type
        /// </summary>
        public static PerLanguageOption<bool> Enabled { get; } = new PerLanguageOption<bool>(AnalyzerName, "Enabled", defaultValue: true);        
    }
}