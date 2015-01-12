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



            throw new NotImplementedException();
        }

        private class SpanToReplace
        {
            public TextSpan Span { get; private set; }

            public string ReplacementText { get; private set; }

            public SpanToReplace(TextSpan span, string replacementText)
            {
                Span = span;
                ReplacementText = replacementText;
            }
        }

        private static List<SpanToReplace> CalculateSpansToRemove(DocumentConditionalRegionInfo info)
        {
            // TODO: Just sort the spans.  I don't think we really need a flat list of regions.  The only reasons regions needed to be sorted were for
            // the sake of intersection (but we can also sort chains for the same effect), and having a sorted list of spans at the end.
            var spans = new List<SpanToReplace>();

            // TODO: If this loop is through each chain, then we know the index in the chain, and we can assert that we have not seen any varying.

            // TODO: Spans don't need to be combined because we know that we're either going to remove all of the directives, or we're going to
            // remove the always disabled directives preceding the varying directives.
            // Two cases for removing all directives: All disabled, or all disabled but one (which could be at the beginning, in the middle, or at the end).

            foreach (var region in info.Regions)
            {
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

                        // Assert that all preceding regions are not varying. If there are varying spans preceding this one, then this span is also varying.
                        for (int i = 0; i < region.IndexInChain; i++)
                        {
                            Debug.Assert(region.Chain[i].State != ConditionalRegionState.Varying);
                        }

                        spans.Add(new SpanToReplace(region.FullSpan, GetReplacementText(region, region.Chain.Count > 3)));
                        break;
                    case ConditionalRegionState.AlwaysEnabled:
                        // We can only safely remove regions that are always enabled if there are no varying regions
                        // following in the chain. Otherwise we have to modify the condition for all the following regions,
                        // and that clutters the code more instead of cleaning it up. 
                        //
                        // When removing a directive that is always enabled, we are also removing all other directives because the
                        // other directives in the chain are necessarily always false.
                        //
                        // This is great because it also means that we don't have to special case removing the bookend directives.

                        // Assert that all preceding regions are always disabled.
                        for (int i = 0; i < region.IndexInChain; i++)
                        {
                            Debug.Assert(region.Chain[i].State == ConditionalRegionState.AlwaysDisabled);
                        }

                        // Check if all following regions in the chain are always disabled
                        // TODO: Could keep track of "canRemoveAlwaysEnabled", just add at end after the scan.
                        bool safeToRemove = true;
                        for (int i = region.IndexInChain + 1; i < region.Chain.Count; i++)
                        {
                            if (region.Chain[i].State != ConditionalRegionState.AlwaysDisabled)
                            {
                                safeToRemove = false;
                            }
                        }

                        if (safeToRemove)
                        {
                            // Remove only the start and end directives themselves.
                            spans.Add(new SpanToReplace(new TextSpan(region.SpanStart, region.StartDirective.FullSpan.End - region.SpanStart), string.Empty));
                            spans.Add(new SpanToReplace(new TextSpan(region.EndDirective.FullSpan.Start, region.SpanEnd - region.EndDirective.FullSpan.Start), string.Empty));
                        }
                        break;
                    case ConditionalRegionState.Varying:
                        // Do not remove this region
                        break;
                }
            }

            // Combine overlapping spans
            var combinedSpans = new List<SpanToReplace>();

            for (int i = 0; i < spans.Count;)
            {
                var current = spans[i];
                var previous = current;
                int j;

                for (j = i + 1; j < spans.Count; j++)
                {
                    var end = spans[j];

                    if (previous.Span.End < end.Span.Start)
                    {
                        break;
                    }

                    // TODO:
                    //current.EndDirective = end.EndDirective;
                    //if (previous.NeedsReplacement || end.NeedsReplacement)
                    //{
                    //    current.NeedsReplacement = true;
                    //}

                    previous = end;
                }

                i = j;
                combinedSpans.Add(current);
            }

            return combinedSpans;

            throw new NotImplementedException();
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
