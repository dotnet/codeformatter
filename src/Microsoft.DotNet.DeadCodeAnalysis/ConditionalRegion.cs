using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public class ConditionalRegion : IComparable<ConditionalRegion>, IEquatable<ConditionalRegion>
    {
        // TODO: These should be syntax references because we only use the span after initialization?
        public DirectiveTriviaSyntax StartDirective { get; private set; }

        public DirectiveTriviaSyntax EndDirective { get; private set; }

        public int SpanStart { get; private set; }

        public int SpanEnd { get; private set; }

        public Location Location { get; private set; }

        public ConditionalRegionState State { get; set; }

        public bool ExplicitlyVaries { get; private set; }

        public ConditionalRegion(DirectiveTriviaSyntax startDirective, DirectiveTriviaSyntax endDirective, IReadOnlyList<ConditionalRegion> chain, int indexInChain, bool explicitlyVaries)
        {
            Debug.Assert(startDirective.SyntaxTree.FilePath == endDirective.SyntaxTree.FilePath);

            StartDirective = startDirective;
            EndDirective = endDirective;

            SpanStart = CalculateSpanStart(startDirective);
            SpanEnd = endDirective.FullSpan.End;
            Location = Location.Create(startDirective.SyntaxTree, new TextSpan(SpanStart, SpanEnd - SpanStart));

            ExplicitlyVaries = explicitlyVaries;
            if (explicitlyVaries)
            {
                State = ConditionalRegionState.Varying;
            }
            else
            {
                State = ConditionalRegionState.AlwaysDisabled;
                var branchingDirective = startDirective as BranchingDirectiveTriviaSyntax;
                if (branchingDirective != null)
                {
                    State = branchingDirective.BranchTaken ?
                        ConditionalRegionState.AlwaysEnabled :
                        ConditionalRegionState.AlwaysDisabled;
                }
            }
        }

        private static int CalculateSpanStart(DirectiveTriviaSyntax startDirective)
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

        public void Intersect(ConditionalRegion other)
        {
            if (!Equals(other))
            {
                return;
            }

            // TODO: If this region becomes varying, then all subsequent regions in the chain must become varying. Fix in intersection op on Chain.
            if (State != other.State)
            {
                State = ConditionalRegionState.Varying;
            }
        }

        public int CompareTo(ConditionalRegion other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = SpanStart - other.SpanStart;
            if (result == 0)
            {
                return SpanEnd - other.SpanEnd;
            }

            return result;
        }

        public bool Equals(ConditionalRegion other)
        {
            return CompareTo(other) == 0;
        }

        public static bool operator== (ConditionalRegion x, ConditionalRegion y)
        {
            if (object.Equals(x, y))
            {
                return true;
            }

            return x.Equals(y);
        }

        public static bool operator!= (ConditionalRegion x, ConditionalRegion y)
        {
            if (object.Equals(x, y))
            {
                return false;
            }

            return x.Equals(y);
        }

        public override string ToString()
        {
            return string.Format("{0}({1}): \"{2}\" : {3}",
                StartDirective.SyntaxTree.FilePath,
                Location.GetLineSpan().StartLinePosition.Line,
                StartDirective.ToString(),
                State);
        }
    }
}
