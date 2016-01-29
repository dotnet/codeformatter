// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    internal abstract class SyntaxFormattingRule : ISyntaxFormattingRule
    {
        public abstract bool SupportsLanguage(string languageName);

        public SyntaxNode Process(SyntaxNode syntaxNode, string languageName)
        {
            switch (languageName)
            {
                case LanguageNames.CSharp:
                    return ProcessCSharp(syntaxNode);
                case LanguageNames.VisualBasic:
                    return ProcessVisualBasic(syntaxNode);
                default:
                    throw new NotSupportedException();
            }
        }

        public virtual SyntaxNode ProcessCSharp(SyntaxNode syntaxNode)
        {
            throw new NotSupportedException();
        }

        public virtual SyntaxNode ProcessVisualBasic(SyntaxNode syntaxNode)
        {
            throw new NotSupportedException();
        }
    }
}
