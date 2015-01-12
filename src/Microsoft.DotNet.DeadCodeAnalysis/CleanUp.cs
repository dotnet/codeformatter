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
        internal static async Task RemoveUnnecessaryRegions(CodeAnalysis.Workspace workspace, IEnumerable<DocumentConditionalRegionInfo> regionInfo, CancellationToken cancellationToken)
        {
            var solution = workspace.CurrentSolution;

            foreach (var info in regionInfo)
            {
                var document = await RemoveUnnecessaryRegions(solution.GetDocument(info.Document.Id), info.Chains, cancellationToken);
                solution = document.Project.Solution;

                PendTfsEdit(document.FilePath);
            }

            if (workspace.TryApplyChanges(solution))
            {
                Console.WriteLine("Solution changes committed.");
            }
        }

        private static async Task<Document> RemoveUnnecessaryRegions(Document document, List<ConditionalRegionChain> chains, CancellationToken cancellationToken)
        {
            if (document == null)
            {
                throw new ArgumentException("document");
            }

            if (chains == null)
            {
                throw new ArgumentException("chains");
            }

            var spans = CalculateSpansToReplace(chains);
            if (spans == null || spans.Count == 0)
            {
                return document;
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

        private static void CalculateSpansToReplace(ConditionalRegionChain chain, List<SpanToReplace> results)
        {
            Debug.Assert(chain.Regions.Count > 0);

            ConditionalRegion startRegion = chain.Regions[0];
            ConditionalRegion enabledRegion = null;
            ConditionalRegion endRegion = chain.Regions[chain.Regions.Count - 1];
            bool endRegionNeedsReplacement = false;

            for (int indexInChain = 0; indexInChain < chain.Regions.Count; indexInChain++)
            {
                var region = chain.Regions[indexInChain];

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
                            endRegion = chain.Regions[indexInChain - 1];
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
