// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingDefaults
    {
        public const string CopyrightRuleName = "CopyrightHeader";
        public const string UnicodeLiteralsRuleName = "UnicodeLiterals";

        private static readonly string[] s_defaultCopyrightHeader =
        {
            "// Licensed to the .NET Foundation under one or more agreements.",
            "// The .NET Foundation licenses this file to you under the MIT license.",
            "// See the LICENSE file in the project root for more information."
        };

        public static readonly ImmutableArray<string> DefaultCopyrightHeader;

        static FormattingDefaults()
        {
            DefaultCopyrightHeader = ImmutableArray.CreateRange(s_defaultCopyrightHeader);
        }
    }
}
