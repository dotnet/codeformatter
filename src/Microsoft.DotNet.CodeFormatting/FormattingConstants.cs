using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingConstants
    {
        private static readonly string[] s_defaultCopyrightHeader =
        {
            "// Copyright (c) Microsoft. All rights reserved.",
            "// Licensed under the MIT license. See LICENSE file in the project root for full license information."
        };

        public static readonly ImmutableArray<string> DefaultCopyrightHeader;

        static FormattingConstants()
        {
            DefaultCopyrightHeader = ImmutableArray.CreateRange(s_defaultCopyrightHeader);
        }
    }
}
