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
    internal static class SyntaxRuleOrder
    {
        public const int HasNoCustomCopyrightHeaderFormattingRule = 1;
        public const int UsingLocationFormattingRule = 2;
        public const int NewLineAboveFormattingRule = 3;
        public const int BraceNewLineRule = 4;
        public const int NonAsciiChractersAreEscapedInLiterals = 5;
        public const int CopyrightHeaderRule = 6;
        public const int RegionsSuckRule = 7;
    }

    // Please keep these values sorted by number, not rule name.    
    internal static class LocalSemanticRuleOrder
    {
        public const int HasNoIllegalHeadersFormattingRule = 1;
        public const int ExplicitVisibilityRule = 2;
        public const int IsFormattedFormattingRule = 3;
        public const int RemoveExplicitThisRule = 4;
        public const int AssertArgumentOrderRule = 5;
    }

    // Please keep these values sorted by number, not rule name.    
    internal static class GlobalSemanticRuleOrder
    {
        public const int PrivateFieldNamingRule = 1;
        public const int MarkReadonlyFieldsRule = 2;
    }
}
