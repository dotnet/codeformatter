// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    // TODO: delete
    internal interface IFormattingRule
    {
        Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Rules which need no semantic information and operate on parse trees only 
    /// </summary>
    internal interface ISyntaxFormattingRule
    {
        SyntaxNode Process(SyntaxNode syntaxRoot);
    }

    /// <summary>
    /// Rules which possibly need semantic information but only operate on a 
    /// specific document.  
    /// </summary>
    internal interface ILocalSemanticFormattingRule
    {
        Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Rules which can affect more than the local document
    /// </summary>
    internal interface IGlobalSemanticFormattingRule
    {
        Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken);
    }
}