// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingDefaults
    {
        public const string UnicodeLiteralsRuleName = "UnicodeLiterals";
        public const string CopyrightRuleName = "Copyright";

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
