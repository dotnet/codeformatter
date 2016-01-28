// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    /// <summary>
    /// Base formatting rule which helps establish which language the rule applies to.
    /// </summary>
    internal interface IFormattingRule
    {
        bool SupportsLanguage(string languageName);
    }

    /// <summary>
    /// Rules which need no semantic information and operate on parse trees only.  
    /// </summary>
    internal interface ISyntaxFormattingRule : IFormattingRule
    {
        SyntaxNode Process(SyntaxNode syntaxRoot, string languageName);
    }

    /// <summary>
    /// Rules which possibly need semantic information but only operate on a specific document.  Also
    /// used for rules that need to see a <see cref="Document"/> and <see cref="SyntaxNode"/> which
    /// are in sync with each other,
    /// </summary>
    internal interface ILocalSemanticFormattingRule : IFormattingRule
    {
        Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Rules which can affect more than the local document
    /// </summary>
    internal interface IGlobalSemanticFormattingRule : IFormattingRule
    {
        Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken);
    }
}
