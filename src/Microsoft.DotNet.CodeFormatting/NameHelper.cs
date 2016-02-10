// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    /// <summary>
    /// Commonly used name functions
    /// </summary>
    internal static class NameHelper
    {
        /// <summary>
        /// Get the full name of a symbol. So a field might look like
        /// "OuterNamespace.Inner.ClassName.FieldName"
        /// </summary>
        /// <param name="symbol">symbol to get name of</param>
        /// <returns>Full display name for a symbol</returns>
        internal static string GetFullName(ISymbol symbol)
        {
            return GetFullName(symbol.ContainingType) + "." + symbol.Name;
        }

        /// <summary>
        /// Get the full name of a type. i.e. "OuterNamespace.Inner.ClassName"
        /// </summary>
        /// <param name="type">type to get name of</param>
        /// <returns>Full display name for a type</returns>
        internal static string GetFullName(INamedTypeSymbol type)
        {
            if (type.ContainingType != null)
            {
                return GetFullName(type.ContainingType) + "+" + type.Name;
            }

            return GetFullName(type.ContainingNamespace) + "." + type.Name;
        }

        /// <summary>
        /// Get the full name of a namespace. i.e. "OuterNamespace.Inner.ClassName"
        /// </summary>
        /// <param name="namespaceSymbol">namespace to get name of</param>
        /// <returns>Full display name for a namespaceSymbol</returns>
        internal static string GetFullName(INamespaceSymbol namespaceSymbol)
        {
            if (namespaceSymbol.ContainingNamespace != null &&
                !namespaceSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                return GetFullName(namespaceSymbol.ContainingNamespace) + "." + namespaceSymbol.Name;
            }

            return namespaceSymbol.Name;
        }
    }
}
