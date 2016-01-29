// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public partial class AnalysisEngine
    {
        public async Task<Document> RemoveUnnecessaryRegions(DocumentConditionalRegionInfo info, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            var changes = CalculateTextChanges(info.Chains);
            if (changes == null || changes.Count == 0)
            {
                return info.Document;
            }

            var newText = await info.Document.GetTextAsync(cancellationToken);

            try
            {
                newText = newText.WithChanges(changes);
            }
            catch (Exception)
            {
                var changesString = new StringBuilder();
                var syntaxTree = info.Document.GetSyntaxTreeAsync(cancellationToken).Result;

                foreach (var change in changes)
                {
                    var lineSpan = Location.Create(syntaxTree, change.Span).GetLineSpan();
                    changesString.AppendFormat("({0}-{1}): {2}", lineSpan.StartLinePosition.Line, lineSpan.EndLinePosition.Line, newText.GetSubText(change.Span).ToString());
                }

                Logger.WriteErrorLine(string.Format("Failed to remove regions from document '{0}':{1}{2}", info.Document.FilePath, Environment.NewLine, changesString.ToString()));
                return info.Document;
            }

            return info.Document.WithText(newText);
        }

        private static List<TextChange> CalculateTextChanges(ImmutableArray<ConditionalRegionChain> chains)
        {
            var changes = new List<TextChange>();

            foreach (var chain in chains)
            {
                CalculateTextChanges(chain, changes);
            }

            changes.Sort(CompareTextChanges);
            return MergeOverlappingRegions(changes);
        }

        public static void CalculateTextChanges(ConditionalRegionChain chain, List<TextChange> changes)
        {
            bool removeEndif = true;

            for (int i = 0; i < chain.Regions.Length; i++)
            {
                var region = chain.Regions[i];
                if (region.State != Tristate.Varying)
                {
                    var startDirective = region.StartDirective;
                    var endDirective = region.EndDirective;
                    string endDirectiveReplacementText = string.Empty;

                    // Remove the start directive
                    changes.Add(new TextChange(new TextSpan(region.SpanStart, region.StartDirective.FullSpan.End - region.SpanStart), string.Empty));

                    if (region.State == Tristate.False)
                    {
                        // Remove the contents of the region
                        changes.Add(new TextChange(new TextSpan(region.StartDirective.FullSpan.End, region.EndDirective.FullSpan.Start - region.StartDirective.FullSpan.End), string.Empty));

                        // Grow the chain until we hit a region that is not always disabled
                        for (int j = i + 1; j < chain.Regions.Length; j++)
                        {
                            var nextRegion = chain.Regions[j];
                            if (nextRegion.State == Tristate.False)
                            {
                                endDirective = nextRegion.EndDirective;
                                region = nextRegion;
                                i = j;

                                // Remove the start directive and the contents of the region
                                changes.Add(new TextChange(new TextSpan(region.SpanStart, region.StartDirective.FullSpan.End - region.SpanStart), string.Empty));
                                changes.Add(new TextChange(new TextSpan(region.StartDirective.FullSpan.End, region.EndDirective.FullSpan.Start - region.StartDirective.FullSpan.End), string.Empty));
                            }
                            else
                            {
                                // If the next region is varying, then the end directive needs replacement
                                if (nextRegion.State == Tristate.Varying)
                                {
                                    endDirectiveReplacementText = GetReplacementText(startDirective, endDirective);
                                    changes.Add(new TextChange(new TextSpan(region.EndDirective.FullSpan.Start, region.SpanEnd - region.EndDirective.FullSpan.Start), endDirectiveReplacementText));
                                }
                                break;
                            }
                        }
                    }
                }
                else
                {
                    removeEndif = false;
                }
            }

            // Remove the final #endif all the other regions have been removed
            if (removeEndif)
            {
                var region = chain.Regions[chain.Regions.Length - 1];
                changes.Add(new TextChange(new TextSpan(region.EndDirective.FullSpan.Start, region.SpanEnd - region.EndDirective.FullSpan.Start), string.Empty));
            }
        }

        private static string GetReplacementText(DirectiveTriviaSyntax startDirective, DirectiveTriviaSyntax endDirective)
        {
            if (startDirective.Kind() == SyntaxKind.IfDirectiveTrivia && endDirective.Kind() == SyntaxKind.ElifDirectiveTrivia)
            {
                var elifDirective = (ElifDirectiveTriviaSyntax)endDirective;
                var elifKeyword = elifDirective.ElifKeyword;
                var newIfDirective = SyntaxFactory.IfDirectiveTrivia(
                    elifDirective.HashToken,
                    SyntaxFactory.Token(elifKeyword.LeadingTrivia, SyntaxKind.IfKeyword, "if", "if", elifKeyword.TrailingTrivia),
                    elifDirective.Condition,
                    elifDirective.EndOfDirectiveToken,
                    elifDirective.IsActive,
                    elifDirective.BranchTaken,
                    elifDirective.ConditionValue);

                return newIfDirective.ToFullString();
            }
            else
            {
                return endDirective.ToFullString();
            }
        }

        private static int CompareTextChanges(TextChange x, TextChange y)
        {
            return x.Span.CompareTo(y.Span);
        }

        private static List<TextChange> MergeOverlappingRegions(List<TextChange> changes)
        {
            // Note: we assume the changes are ordered by CompareTextChanges
            var newChanges = new List<TextChange>();

            for (int i = 0; i < changes.Count; i++)
            {
                TextChange change = changes[i];
                for (int j = i + 1; j < changes.Count; j++)
                {
                    TextChange nextChange = changes[j];

                    if (nextChange.Span.Start <= change.Span.End &&
                        nextChange.Span.End >= change.Span.End)
                    {
                        // This change overlaps but is not contained within the previous change.
                        // In the case that this change ends where the previous change ends, we need to take
                        // the replacement text of this change, because it is possible for end directives to
                        // need non-empty replacement.
                        change = new TextChange(new TextSpan(change.Span.Start, nextChange.Span.End - change.Span.Start), nextChange.NewText);
                        i = j;
                    }
                }

                newChanges.Add(change);
            }

            return newChanges;
        }
    }
}
