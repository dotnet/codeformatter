using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public static class CleanUp
    {
        public static async Task<Document> RemoveUnnecessaryRegions(DocumentConditionalRegionInfo regionInfo, CancellationToken cancellationToken)
        {
            if (regionInfo == null)
            {
                throw new ArgumentException("regionInfo");
            }

            var document = regionInfo.Document;
            var spans = CalculateSpansToRemove(regionInfo);
            if (spans == null || spans.Count == 0)
            {
                return regionInfo.Document;
            }

            // Remove the unnecessary spans from the end of the document to the beginning to preserve character positions
            var newText = await document.GetTextAsync(cancellationToken);

            for (int i = spans.Count - 1; i >= 0; --i)
            {
                var span = spans[i];
                newText = newText.Replace(span.Span, span.ReplacementText);
            }

            return document.WithText(newText);
        }

        private class SpanToReplace : IComparable<SpanToReplace>
        {
            public TextSpan Span { get; private set; }

            public string ReplacementText { get; private set; }

            public SpanToReplace(int start, int end, string replacementText)
            {
                if (end < start)
                {
                    throw new ArgumentOutOfRangeException("end");
                }

                Span = new TextSpan(start, end - start);
                ReplacementText = replacementText;
            }

            public int CompareTo(SpanToReplace other)
            {
                return Span.CompareTo(other.Span);
            }
        }

        private static List<SpanToReplace> CalculateSpansToRemove(DocumentConditionalRegionInfo info)
        {
            var spans = new List<SpanToReplace>();

            // TODO: Spans don't need to be combined because we know that we're either going to remove all of the directives, or we're going to
            // remove the always disabled directives preceding the varying directives.
            // Two cases for removing all directives: All disabled, or all disabled but one (which could be at the beginning, in the middle, or at the end).

            // TODO: A chain struct could have a GetUnnecessarySpans() method

            foreach (var chain in info.Chains)
            {
                var chainSpans = CalculateSpansToRemove(chain);
                spans.AddRange(chainSpans);
            }

            spans.Sort();

            return spans;
        }

        private static void CalculateSpansToRemove(List<ConditionalRegion> chain, List<SpanToReplace> results)
        {
            Debug.Assert(chain.Count > 0);

            ConditionalRegion startRegion = chain[0];
            ConditionalRegion enabledRegion = null;
            ConditionalRegion endRegion = chain[chain.Count - 1];
            bool endRegionNeedsReplacement = false;

            for (int indexInChain = 0; indexInChain < chain.Count; indexInChain++)
            {
                var region = chain[indexInChain];

                switch (region.State)
                {
                    case ConditionalRegionState.AlwaysDisabled:
                        // We can always safely remove regions that are always false.
                        // In a chain of #if #elif #else directives, each successive condition depends on the preceding
                        // condition being false. So each condition can be written: !PRECEDING_CONDITION && THIS_CONDITION
                        // When the preceding condition is always known to be false, the expression simplifies to
                        // THIS_CONDITION and the preceding region can be removed altogether.
                        //
                        // So, remove the start and end directives as well as the contents of the region.
                        break;
                    case ConditionalRegionState.AlwaysEnabled:
                        // We can only safely remove regions that are always enabled if there are no varying regions in the chain.
                        // There cannot be varying regions preceding this one, because then this region would be implicitly varying.
                        // If there are varying regions following this one, then it means that we are missing data about another build
                        // configuration in which this region is not enabled, in which case this region is actually varying.
                        //
                        // In other words, when removing a directive that is always enabled, we are also removing all other directives
                        // because the other directives in the chain are necessarily always disabled.
                        enabledRegion = region;
                        break;
                    case ConditionalRegionState.Varying:
                        // If there is an always enabled region in this chain, but there is a subsequent region that is
                        // varying, then then it means that we are missing data about another build configuration in
                        // which the enabled region is disabled, in which case it is actually varying. Since the
                        // enabled region depends on the conditions of its preceding regions, we may be missing data
                        // about any of those conditions. To be safe, do not remove any regions in this chain.
                        if (enabledRegion != null)
                        {
                            return;
                        }
                        else
                        {
                            // All preceding regions are always disabled. Do not remove this region or any that follow.
                            endRegion = chain[indexInChain - 1];
                            endRegionNeedsReplacement = true;
                            goto ScanFinished;
                        }
                }
            }

        ScanFinished:
            if (startRegion == enabledRegion)
            {
                // Only remove the start directive of the start region
                results.Add(new SpanToReplace(startRegion.SpanStart, enabledRegion.StartDirective.FullSpan.End, string.Empty));
            }
            else if (enabledRegion != null)
            {
                // Remove all regions from the start region up to the enabled region, but only remove the start
                // directive of the enabled region.
                results.Add(new SpanToReplace(startRegion.SpanStart, enabledRegion.StartDirective.FullSpan.End, string.Empty));
            }

            if (endRegion == enabledRegion)
            {
                // Only remove the end directive of the end region
                results.Add(new SpanToReplace(endRegion.EndDirective.FullSpan.Start, endRegion.SpanEnd, string.Empty));
            }
            else if (enabledRegion != null)
            {
                // Remove all regions from the enabled region up to and including the end region, but only remove
                // the end directive of the enabled region.
                results.Add(new SpanToReplace(enabledRegion.EndDirective.FullSpan.Start, endRegion.SpanEnd, string.Empty));
            }
            else if (startRegion.State == ConditionalRegionState.AlwaysDisabled)
            {
                // There is no enabled region. Remove all disabled regions up to and including the end region.
                results.Add(new SpanToReplace(startRegion.SpanStart, endRegion.SpanEnd, GetReplacementText(endRegion, endRegionNeedsReplacement)));
            }
        }

        public static async Task<Document> RemoveInactiveDirectives(Document document, IList<DirectiveTriviaSyntax> directives, CancellationToken cancellationToken)
        {
            if (directives == null)
            {
                // No directives to remove
                return document;
            }

            var originalText = await document.GetTextAsync(cancellationToken);

            var spansToRemove = CalculateInactiveSpansToRemove(directives.ToArray(), originalText);
            if (spansToRemove == null || spansToRemove.Count == 0)
            {
                return document;
            }

            // Remove the unnecessary spans from the end of the document to the beginning to preserve character positions
            var newText = originalText;

            for (int i = spansToRemove.Count - 1; i >= 0; --i)
            {
                var span = spansToRemove[i];
                newText = newText.Replace(span.FullSpan, GetReplacementText(span));
            }

            return document.WithText(newText);
        }

        private static List<SpanToRemove> CalculateInactiveSpansToRemove(IList<DirectiveTriviaSyntax> unnecessaryDirectives, SourceText text)
        {
            var spans = new List<SpanToRemove>();
            int unnecessaryIndex = 0;

            while (unnecessaryIndex < unnecessaryDirectives.Count)
            {
                var startDirective = unnecessaryDirectives[unnecessaryIndex];
                var linkedDirectives = startDirective.GetLinkedDirectives();

                var nextLinkedIndex = linkedDirectives.IndexOf(startDirective) + 1;
                if (nextLinkedIndex >= linkedDirectives.Count)
                {
                    // A branching directive (#if, #elif, #else) should always be followed by another directive.
                    // If this is not the case, there is an error in the code, so don't bother to remove anything.
                    return null;
                }

                // If the next linked directive is also the next unnecessary directive, we can continue to grow this span to remove.
                var endDirective = linkedDirectives[nextLinkedIndex];
                int directivesInSpan = 2;

                while (++unnecessaryIndex < unnecessaryDirectives.Count && endDirective == unnecessaryDirectives[unnecessaryIndex])
                {
                    if (++nextLinkedIndex >= linkedDirectives.Count)
                    {
                        // Since we know endDirective is an unnecessary branching directive, we know it must be followed by another directive.
                        // Again, if this is not the case, there is an error in the code, so don't bother to remove anything.
                        return null;
                    }

                    endDirective = linkedDirectives[nextLinkedIndex];
                    ++directivesInSpan;
                }

                // If this span includes all but one region in the set of linked directives, add a span to remove the book-ending
                // #if or #endif directive.
                DirectiveTriviaSyntax endIfDirective = null;

                if (directivesInSpan == linkedDirectives.Count - 1)
                {
                    if (startDirective.CSharpKind() == SyntaxKind.IfDirectiveTrivia)
                    {
                        endIfDirective = linkedDirectives[linkedDirectives.Count - 1];
                        Debug.Assert(endIfDirective.CSharpKind() == SyntaxKind.EndIfDirectiveTrivia);
                    }
                    else
                    {
                        var ifDirective = linkedDirectives[0];
                        Debug.Assert(ifDirective.CSharpKind() == SyntaxKind.IfDirectiveTrivia);
                        spans.Add(new SpanToRemove(ifDirective, ifDirective));
                    }
                }

                // Add the span we previously calculated followed by the book-ending #endif directive to preserve prefix document ordering.
                spans.Add(new SpanToRemove(startDirective, endDirective, needsReplacement: linkedDirectives.Count > 3));

                if (endIfDirective != null)
                {
                    spans.Add(new SpanToRemove(endIfDirective, endIfDirective));
                }
            }

            // Combine overlapping spans
            var combinedSpans = new List<SpanToRemove>();

            for (int i = 0; i < spans.Count;)
            {
                var newSpan = spans[i];
                var previousSpan = newSpan;
                int j;

                for (j = i + 1; j < spans.Count; j++)
                {
                    var endSpan = spans[j];

                    if (previousSpan.End < endSpan.Start)
                    {
                        break;
                    }

                    newSpan.EndDirective = endSpan.EndDirective;
                    if (previousSpan.NeedsReplacement || endSpan.NeedsReplacement)
                    {
                        newSpan.NeedsReplacement = true;
                    }

                    previousSpan = endSpan;
                }

                i = j;
                combinedSpans.Add(newSpan);
            }

            return combinedSpans;
        }

        private static string GetReplacementText(SpanToRemove span)
        {
            if (span.NeedsReplacement)
            {
                if (span.StartDirective.CSharpKind() == SyntaxKind.IfDirectiveTrivia)
                {
                    Debug.Assert(span.EndDirective.CSharpKind() == SyntaxKind.ElifDirectiveTrivia);
                    var elifDirective = (ElifDirectiveTriviaSyntax)span.EndDirective;
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
                    return span.EndDirective.ToFullString();
                }
            }

            return string.Empty;
        }

        private static string GetReplacementText(ConditionalRegion region, bool needsReplacement)
        {
            if (needsReplacement)
            {
                if (region.StartDirective.CSharpKind() == SyntaxKind.IfDirectiveTrivia)
                {
                    Debug.Assert(region.EndDirective.CSharpKind() == SyntaxKind.ElifDirectiveTrivia);
                    var elifDirective = (ElifDirectiveTriviaSyntax)region.EndDirective;
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
                    return region.EndDirective.ToFullString();
                }
            }

            return string.Empty;
        }

        private static void PendTfsEdit(string filePath)
        {
            var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(filePath);
            if (workspaceInfo != null)
            {
                var server = new TfsTeamProjectCollection(workspaceInfo.ServerUri);
                var workspace = workspaceInfo.GetWorkspace(server);
                workspace.PendEdit(filePath);
            }
        }

        private class SpanToRemove
        {
            private DirectiveTriviaSyntax m_startDirective;
            private DirectiveTriviaSyntax m_endDirective;

            public DirectiveTriviaSyntax StartDirective
            {
                get { return m_startDirective; }
                set
                {
                    m_startDirective = value;
                    Start = CalculateStart(value);
                }
            }

            public DirectiveTriviaSyntax EndDirective
            {
                get { return m_endDirective; }
                set
                {
                    m_endDirective = value;
                    End = m_endDirective.FullSpan.End;
                }
            }

            public int Start { get; private set; }

            public int End { get; private set; }

            public bool NeedsReplacement;

            public TextSpan FullSpan { get { return new TextSpan(Start, End - Start); } }

            public SpanToRemove(DirectiveTriviaSyntax startDirective, DirectiveTriviaSyntax endDirective, bool needsReplacement = false)
            {
                StartDirective = startDirective;
                EndDirective = endDirective;
                NeedsReplacement = needsReplacement;
            }

            private static int CalculateStart(DirectiveTriviaSyntax startDirective)
            {
                int start = startDirective.FullSpan.Start;

                // Consume whitespace trivia preceding the start directive
                var leadingTrivia = startDirective.ParentTrivia.Token.LeadingTrivia;
                var triviaIndex = leadingTrivia.IndexOf(startDirective.ParentTrivia);
                if (triviaIndex > 0)
                {
                    var previousTrivia = leadingTrivia[triviaIndex - 1];
                    if (previousTrivia.CSharpKind() == SyntaxKind.WhitespaceTrivia)
                    {
                        start = previousTrivia.FullSpan.Start;
                    }
                }

                return start;
            }
        }
    }
}
