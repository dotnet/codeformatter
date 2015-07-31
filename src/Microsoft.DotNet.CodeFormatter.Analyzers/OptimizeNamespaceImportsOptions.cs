// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Options.Providers;
 
namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportOptionsProvider, Shared]
    internal class OptimizeNamespaceImportsOptions : IOptionsProvider
    {        
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                RemoveUnnecessaryImports,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = AnalyzerIds.OptimizeNamespaceImports + "." + "OptimizeNamespaceImports";

        /// <summary>
        /// This option enables removal of namespace imports that aren't required for compilation
        /// </summary>
        public static Option<bool> RemoveUnnecessaryImports { get; } = new Option<bool>(AnalyzerName, "RemoveUnnecessaryImports", true);
    }
}