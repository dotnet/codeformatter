// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [SyntaxRuleOrder(SyntaxRuleOrder.HasCopyrightHeaderFormattingRule)]
    internal sealed partial class CopyrightHeaderRule : SyntaxFormattingRule, ISyntaxFormattingRule
    {
        private readonly Options _options;

        [ImportingConstructor]
        internal CopyrightHeaderRule(Options options)
        {
            _options = options;
        }

        public override bool SupportsLanguage(string languageName)
        {
            return languageName == LanguageNames.CSharp || languageName == LanguageNames.VisualBasic;
        }

        public override SyntaxNode ProcessCSharp(SyntaxNode syntaxNode)
        {
            return (new CSharpRule(_options)).Process(syntaxNode);
        }

        public override SyntaxNode ProcessVisualBasic(SyntaxNode syntaxNode)
        {
            return (new VisualBasicRule(_options)).Process(syntaxNode);
        }

        private static string GetCommentText(string line)
        {
            if (line.StartsWith("'"))
            {
                return line.Substring(1).TrimStart();
            }

            if (line.StartsWith("//"))
            {
                return line.Substring(2).TrimStart();
            }

            return line;
        }
    }
}