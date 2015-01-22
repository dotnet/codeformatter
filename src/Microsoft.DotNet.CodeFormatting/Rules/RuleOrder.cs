// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    // Please keep these values sorted by number, not rule name.    
    internal static class RuleOrder
    {
        public const int HasNoCustomCopyrightHeaderFormattingRule = 1;
        public const int HasNoIllegalHeadersFormattingRule = 2;
        public const int HasCopyrightHeaderFormattingRule = 3;
        public const int HasUsingsOutsideOfNamespaceFormattingRule = 4;
        public const int HasNewLineBeforeFirstUsingFormattingRule = 5;
        public const int HasNewLineBeforeFirstNamespaceFormattingRule = 6;
        public const int BraceNewLineRule = 7;
        public const int ExplicitVisibilityRule = 8;
        public const int PrivateFieldNamingRule = 9;
        public const int IsFormattedFormattingRule = 10;
        public const int UsesXunitForTestsFormattingRule = 11;
        public const int NonAsciiChractersAreEscapedInLiterals = 12;
        public const int RemoveExplicitThisRule = 13;
    }
}
