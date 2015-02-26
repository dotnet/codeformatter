using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public class ConditionalRegion : IComparable<ConditionalRegion>, IEquatable<ConditionalRegion>
    {
        public DirectiveTriviaSyntax StartDirective { get; private set; }

        public DirectiveTriviaSyntax EndDirective { get; private set; }

        public int SpanStart { get; private set; }

        public int SpanEnd { get; private set; }

        public Location Location { get; private set; }

        public Tristate State { get; private set; }

        public ConditionalRegion(DirectiveTriviaSyntax startDirective, DirectiveTriviaSyntax endDirective, IReadOnlyList<ConditionalRegion> chain, int indexInChain, Tristate state)
        {
            Debug.Assert(startDirective.SyntaxTree.FilePath == endDirective.SyntaxTree.FilePath);

            StartDirective = startDirective;
            EndDirective = endDirective;

            SpanStart = CalculateSpanStart(startDirective);
            SpanEnd = endDirective.FullSpan.End;
            Location = Location.Create(startDirective.SyntaxTree, new TextSpan(SpanStart, SpanEnd - SpanStart));
            State = state;
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

        public override bool Equals(object obj)
        {
            return Equals(obj as ConditionalRegion);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
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

            return !x.Equals(y);
        }

        public override string ToString()
        {
            string stateString =
                State == Tristate.False ? "Always Disabled" :
                State == Tristate.True ? "Always Enabled" :
                "Varying";

            return string.Format("{0}({1}): \"{2}\" : {3}",
                StartDirective.SyntaxTree.FilePath,
                Location.GetLineSpan().StartLinePosition.Line,
                StartDirective.ToString(),
                stateString);
        }
    }
}
