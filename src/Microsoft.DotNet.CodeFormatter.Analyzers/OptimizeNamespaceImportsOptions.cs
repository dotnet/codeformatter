// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.Options;
 
namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(IOptionsProvider)), Shared]
    internal class OptimizeNamespaceImportsOptions : IOptionsProvider
    {        
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                Enabled,
                RemoveUnnecessaryImports,
                SortImports,
                PlaceImportsOutsideNamespaceDeclaration,
                PlaceSystemImportsFirst,
                InsertLineBetweenImportGroups
            }.ToImmutableArray();
        }

        private const string AnalyzerName = AnalyzerIds.OptimizeNamespaceImports + "." + "OptimizeNamespaceImports";

        /// <summary>
        /// Enable namespace import optimization.
        /// </summary>
        public static PerLanguageOption<bool> Enabled { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(Enabled), defaultValue: true);

        /// <summary>
        /// Remove imports that aren't required for compilation.
        /// </summary>
        public static PerLanguageOption<bool> RemoveUnnecessaryImports { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(RemoveUnnecessaryImports), defaultValue: true);

        /// <summary>
        /// Sort import directives by namespace
        /// </summary>
        public static PerLanguageOption<bool> SortImports { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(SortImports), defaultValue: true);

        /// <summary>
        /// Place import directives outside namespace declaration (or within if value is false).
        /// </summary>
        public static PerLanguageOption<bool> PlaceImportsOutsideNamespaceDeclaration { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(PlaceImportsOutsideNamespaceDeclaration), defaultValue: true);

        /// <summary>
        /// Place system namespaces first when writing import directives.
        /// </summary>
        public static PerLanguageOption<bool> PlaceSystemImportsFirst { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(PlaceSystemImportsFirst), defaultValue: true);

        /// <summary>
        /// Insert a line-break between import first-order namespace groups, System.*, Microsoft.*, etc.
        /// </summary>
        public static PerLanguageOption<bool> InsertLineBetweenImportGroups { get; } = new PerLanguageOption<bool>(AnalyzerName, nameof(InsertLineBetweenImportGroups), defaultValue: true);
    }
}