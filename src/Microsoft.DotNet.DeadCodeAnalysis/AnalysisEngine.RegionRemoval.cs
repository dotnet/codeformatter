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

            var changes = CalculateTextChanges(info.Chains);
            if (changes == null || changes.Count == 0)
            {
                return info.Document;
            }

            // Remove the unnecessary spans from the end of the document to the beginning to preserve character positions
            var newText = await info.Document.GetTextAsync(cancellationToken);
            //newText = newText.WithChanges(changes);

            for (int i = changes.Count - 1; i >= 0; --i)
            {
                var change = changes[i];
                var textToReplace = newText.GetSubText(change.Span);
                Console.WriteLine("Replacing: " + textToReplace);
                newText = newText.Replace(change.Span, change.NewText);
            }

            return info.Document.WithText(newText);
        }

        private static int CompareTextChanges(TextChange x, TextChange y)
        {
            return x.Span.CompareTo(y.Span);
        }

        private static List<TextChange> CalculateTextChanges(List<ConditionalRegionChain> chains)
        {
            var changes = new List<TextChange>();

            // TODO: A chain struct could have a GetUnnecessarySpans() method

            foreach (var chain in chains)
            {
                CalculateTextChanges(chain, changes);
            }

            changes.Sort(CompareTextChanges);

            return changes;
        }

        public static void CalculateTextChanges(ConditionalRegionChain chain, List<TextChange> changes)
        {
            bool removeEndif = true;

            for (int i = 0; i < chain.Regions.Count; i++)
            {
                var region = chain.Regions[i];
                if (region.State != ConditionalRegionState.Varying)
                {
                    // Remove the start directive
                    changes.Add(new TextChange(new TextSpan(region.SpanStart, region.StartDirective.FullSpan.End - region.SpanStart), string.Empty));

                    string endDirectiveReplacementText = string.Empty;

                    if (region.State == ConditionalRegionState.AlwaysDisabled)
                    {
                        // Remove the contents of the region
                        changes.Add(new TextChange(new TextSpan(region.StartDirective.FullSpan.End, region.EndDirective.FullSpan.Start - region.StartDirective.FullSpan.End), string.Empty));
                    }

                    // If the next region is varying, then the end directive needs replacement
                    if (i + 1 < chain.Regions.Count &&
                        chain.Regions[i + 1].State == ConditionalRegionState.Varying)
                    {
                        endDirectiveReplacementText = GetReplacementText(region);
                        changes.Add(new TextChange(new TextSpan(region.EndDirective.FullSpan.Start, region.SpanEnd - region.EndDirective.FullSpan.Start), endDirectiveReplacementText));
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
                var region = chain.Regions[chain.Regions.Count - 1];
                changes.Add(new TextChange(new TextSpan(region.EndDirective.FullSpan.Start, region.SpanEnd - region.EndDirective.FullSpan.Start), string.Empty));
            }
        }

        private static string GetReplacementText(ConditionalRegion region)
        {
            if (region.StartDirective.CSharpKind() == SyntaxKind.IfDirectiveTrivia && region.EndDirective.CSharpKind() == SyntaxKind.ElifDirectiveTrivia)
            {
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
