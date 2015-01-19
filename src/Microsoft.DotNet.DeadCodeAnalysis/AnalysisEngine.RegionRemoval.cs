using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public partial class AnalysisEngine
    {
        public async Task<Document> RemoveUnnecessaryRegions(DocumentConditionalRegionInfo info, CancellationToken cancellationToken = default(CancellationToken))
        {
            Debug.Assert(info != null);

            var spans = CalculateSpansToReplace(info.Chains);
            if (spans == null || spans.Count == 0)
            {
                return info.Document;
            }

            // Remove the unnecessary spans from the end of the document to the beginning to preserve character positions
            var newText = await info.Document.GetTextAsync(cancellationToken);

            for (int i = spans.Count - 1; i >= 0; --i)
            {
                var span = spans[i];
                newText = newText.Replace(span.Span, span.ReplacementText);
            }

            return info.Document.WithText(newText);
        }

        private static List<SpanToReplace> CalculateSpansToReplace(List<ConditionalRegionChain> chains)
        {
            var spans = new List<SpanToReplace>();

            // TODO: A chain struct could have a GetUnnecessarySpans() method

            foreach (var chain in chains)
            {
                CalculateSpansToReplace(chain, spans);
            }

            spans.Sort();

            return spans;
        }

        private static void CalculateSpansToReplace(ConditionalRegionChain chain, List<SpanToReplace> spans)
        {
            Debug.Assert(chain.Regions.Count > 0);

            ConditionalRegion startRegion = chain.Regions[0];
            ConditionalRegion enabledRegion = null;
            ConditionalRegion endRegion = chain.Regions[chain.Regions.Count - 1];
            bool endRegionNeedsReplacement = false;

            for (int i = 0; i < chain.Regions.Count; i++)
            {
                var region = chain.Regions[i];

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
                        if (enabledRegion == null)
                        {
                            // Only allow the first always enabled region we come across to be enabled because that is how
                            // the C# compiler works.
                            // TODO: This could be calculated earlier.  If a given region is always enabled, then none of the following regions can ever be enabled
                            // as interpreted by the compiler.
                            enabledRegion = region;
                        }
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
                        else if (i == 0)
                        {
                            // All regions in this chain are varying
                            return;
                        }
                        else
                        {
                            // All preceding regions are always disabled. Do not remove this region or any that follow.
                            endRegion = chain.Regions[i - 1];
                            endRegionNeedsReplacement = true;
                            goto ScanFinished;
                        }
                }
            }

            ScanFinished:
            if (startRegion == enabledRegion)
            {
                // Only remove the start directive of the start region
                spans.Add(new SpanToReplace(startRegion.SpanStart, enabledRegion.StartDirective.FullSpan.End, string.Empty));
            }
            else if (enabledRegion != null)
            {
                // Remove all regions from the start region up to the enabled region, but only remove the start
                // directive of the enabled region.
                spans.Add(new SpanToReplace(startRegion.SpanStart, enabledRegion.StartDirective.FullSpan.End, string.Empty));
            }

            if (endRegion == enabledRegion)
            {
                // Only remove the end directive of the end region
                spans.Add(new SpanToReplace(endRegion.EndDirective.FullSpan.Start, endRegion.SpanEnd, string.Empty));
            }
            else if (enabledRegion != null)
            {
                // Remove all regions from the enabled region up to and including the end region, but only remove
                // the end directive of the enabled region.
                spans.Add(new SpanToReplace(enabledRegion.EndDirective.FullSpan.Start, endRegion.SpanEnd, string.Empty));
            }
            else if (startRegion.State == ConditionalRegionState.AlwaysDisabled)
            {
                // There is no enabled region. Remove all disabled regions up to and including the end region.
                spans.Add(new SpanToReplace(startRegion.SpanStart, endRegion.SpanEnd, GetReplacementText(endRegion, endRegionNeedsReplacement)));
            }
        }

        private static string GetReplacementText(ConditionalRegion region, bool needsReplacement)
        {
            if (needsReplacement)
            {
                // TODO: Fix this
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
    }
}
