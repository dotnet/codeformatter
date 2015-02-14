// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    internal partial class CopyrightHeaderRule
    {
        private sealed class VisualBasicRule
        {
            private readonly Options _options;

            internal VisualBasicRule(Options options)
            {
                _options = options;
            }

            internal SyntaxNode Process(SyntaxNode syntaxNode)
            {
                if (_options.CopyrightHeader.IsDefaultOrEmpty)
                {
                    return syntaxNode;
                }

                if (HasCopyrightHeader(syntaxNode))
                    return syntaxNode;

                return AddCopyrightHeader(syntaxNode);
            }

            private bool HasCopyrightHeader(SyntaxNode syntaxNode)
            {
                var leadingComments = syntaxNode.GetLeadingTrivia().Where(t => t.VBKind() == SyntaxKind.CommentTrivia).ToArray();
                var header = _options.CopyrightHeader;
                if (leadingComments.Length < header.Length)
                    return false;

                return leadingComments.Take(header.Length)
                                      .Select(t => GetCommentText(t.ToFullString()))
                                      .SequenceEqual(header.Select(GetCommentText));
            }

            private SyntaxNode AddCopyrightHeader(SyntaxNode syntaxNode)
            {
                var newTrivia = GetCopyrightHeader().Concat(syntaxNode.GetLeadingTrivia());
                return syntaxNode.WithLeadingTrivia(newTrivia);
            }

            private IEnumerable<SyntaxTrivia> GetCopyrightHeader()
            {
                foreach (var headerLine in _options.CopyrightHeader)
                {
                    yield return SyntaxFactory.CommentTrivia("' " + GetCommentText(headerLine));
                    yield return SyntaxFactory.CarriageReturnLineFeed;
                }
            }
        }
    }
}
