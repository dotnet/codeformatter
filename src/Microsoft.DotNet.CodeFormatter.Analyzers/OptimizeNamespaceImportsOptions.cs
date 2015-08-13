// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.Options;
 
namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [ExportOptionsProvider, Shared]
    internal class OptimizeNamespaceImportsOptions : IOptionsProvider
    {        
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                Enabled,
                RemoveUnnecessaryImports,
                SortImports,
                MoveImportsOutsideNamespaceDeclaration,
                PlaceSystemImportsFirst,
                InsertLineBetweenImportGroups
            }.ToImmutableArray();
        }

        private const string AnalyzerName = AnalyzerIds.OptimizeNamespaceImports + "." + "OptimizeNamespaceImports";

        /// <summary>
        /// Enable namepsace optimization
        /// </summary>
        public static PerLanguageOption<bool> Enabled { get; } = new PerLanguageOption<bool>(AnalyzerName, "Enabled", defaultValue: true);

        /// <summary>
        /// Remove  imports that aren't required for compilation
        /// </summary>
        public static PerLanguageOption<bool> RemoveUnnecessaryImports { get; } = new PerLanguageOption<bool>(AnalyzerName, "RemoveUnnecessaryImports", defaultValue: true);

        /// <summary>
        /// Sort namespaces
        /// </summary>
        public static PerLanguageOption<bool> SortImports { get; } = new PerLanguageOption<bool>(AnalyzerName, "SortImports", defaultValue: true);

        /// <summary>
        /// Write namepsaces outside namespace declaration (or within if value is false)
        /// </summary>
        public static PerLanguageOption<bool> MoveImportsOutsideNamespaceDeclaration { get; } = new PerLanguageOption<bool>(AnalyzerName, "MoveImportsOutsideNamespaceDeclaration", defaultValue: true);

        /// <summary>
        /// Place system namespaces first when writing import directives
        /// </summary>
        public static PerLanguageOption<bool> PlaceSystemImportsFirst { get; } = new PerLanguageOption<bool>(AnalyzerName, "PlaceSystemImportsFirst", defaultValue: true);
        /// <summary>
        /// Insert a line-break between first-order namespaces groups, System.*, Microsoft.*, etc.
        /// </summary>
        public static PerLanguageOption<bool> InsertLineBetweenImportGroups { get; } = new PerLanguageOption<bool>(AnalyzerName, "InsertLineBetweenImportGroups", defaultValue: true);
    }
}