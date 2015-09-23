// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.Options;
using Microsoft.DotNet.CodeFormatting.Rules;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    [Export(typeof(IOptionsProvider)), Shared]
    internal class RuleOptions : IOptionsProvider
    {
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                BraceNewLineEnabled,
                CopyrightHeaderEnabled,
                ExplicitThisEnabled,
                ExplicitVisibilityEnabled,
                FieldNamesEnabled,
                FormatDocumentEnabled,
                RemoveCustomCopyrightEnabled,
                RemoveIllegalHeadersEnabled,
                MarkReadonlyFieldsEnabled,
                NewLineAboveEnabled,
                UnicodeLiteralsEnabled,
                UsingLocationEnabled
            }.ToImmutableArray();
        }

        private const string _feature = "CodeFormatterRules";

        public static PerLanguageOption<bool> BraceNewLineEnabled { get; } = new PerLanguageOption<bool>(_feature, BraceNewLineRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> CopyrightHeaderEnabled { get; } = new PerLanguageOption<bool>(_feature, CopyrightHeaderRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> ExplicitThisEnabled { get; } = new PerLanguageOption<bool>(_feature, ExplicitThisRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> ExplicitVisibilityEnabled { get; } = new PerLanguageOption<bool>(_feature, ExplicitVisibilityRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> FieldNamesEnabled { get; } = new PerLanguageOption<bool>(_feature, PrivateFieldNamingRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> FormatDocumentEnabled { get; } = new PerLanguageOption<bool>(_feature, FormatDocumentRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> RemoveCustomCopyrightEnabled { get; } = new PerLanguageOption<bool>(_feature, RemoveCustomCopyrightRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> RemoveIllegalHeadersEnabled { get; } = new PerLanguageOption<bool>(_feature, HasNoIllegalHeadersRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> MarkReadonlyFieldsEnabled { get; } = new PerLanguageOption<bool>(_feature, MarkReadonlyFieldsRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> NewLineAboveEnabled { get; } = new PerLanguageOption<bool>(_feature, NewLineAboveRule.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> UnicodeLiteralsEnabled { get; } = new PerLanguageOption<bool>(_feature, NonAsciiCharactersAreEscapedInLiterals.Name + "." + "Enabled", defaultValue: true);
        public static PerLanguageOption<bool> UsingLocationEnabled { get; } = new PerLanguageOption<bool>(_feature, UsingLocationRule.Name + "." + "Enabled", defaultValue: true);
    }
}