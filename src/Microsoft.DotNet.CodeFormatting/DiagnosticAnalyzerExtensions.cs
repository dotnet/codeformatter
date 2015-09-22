// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatting
{
    internal static class DiagnosticAnalyzerExtensions
    {
        /// <summary>
        /// Return a value indicating whether the specified analyzer supports the specified language.
        /// </summary>
        /// <param name="analyzer">
        /// The analyzer to be examined.
        /// </param>
        /// <param name="language">
        /// The name of the language.
        /// </param>
        /// <returns>
        /// <code>true</code> if <paramref name="analyzer"/> supports <paramref name="language"/>;
        /// otherwise <code>false</code>.
        /// </returns>
        internal static bool SupportsLanguage(this DiagnosticAnalyzer analyzer, string language)
        {
            DiagnosticAnalyzerAttribute attribute = analyzer.GetType().GetCustomAttribute<DiagnosticAnalyzerAttribute>(inherit: true);

            // Every analyzer should have this attribute, but behave reasonably if it does not.
            if (attribute == null)
            {
                return false;
            }

            return attribute.Languages.Contains(language);
        }
    }
}
