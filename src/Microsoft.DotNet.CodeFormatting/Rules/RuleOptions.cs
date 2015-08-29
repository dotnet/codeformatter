// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.Options;

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

        public static PerLanguageOption<bool> BraceNewLineEnabled { get; } = new PerLanguageOption<bool>(_feature, "BraceNewLine.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> CopyrightHeaderEnabled { get; } = new PerLanguageOption<bool>(_feature, "CopyrightHeader.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> ExplicitThisEnabled { get; } = new PerLanguageOption<bool>(_feature, "ExplicitThis.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> ExplicitVisibilityEnabled { get; } = new PerLanguageOption<bool>(_feature, "ExplicitVisibility.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> FormatDocumentEnabled { get; } = new PerLanguageOption<bool>(_feature, "FormatDocument.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> RemoveCustomCopyrightEnabled { get; } = new PerLanguageOption<bool>(_feature, "RemoveCustomCopyright.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> RemoveIllegalHeadersEnabled { get; } = new PerLanguageOption<bool>(_feature, "RemoveIllegalHeaders.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> MarkReadonlyFieldsEnabled { get; } = new PerLanguageOption<bool>(_feature, "MarkReadonly.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> NewLineAboveEnabled { get; } = new PerLanguageOption<bool>(_feature, "NewLineAbove.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> UnicodeLiteralsEnabled { get; } = new PerLanguageOption<bool>(_feature, "UnicodeLiterals.Enabled", defaultValue: true);
        public static PerLanguageOption<bool> UsingLocationEnabled { get; } = new PerLanguageOption<bool>(_feature, "UsingLocation.Enabled", defaultValue: true);
    }
}