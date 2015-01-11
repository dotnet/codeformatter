using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    internal static class DirectiveTriviaSyntaxExtensions
    {
        /// <summary>
        /// Returns a list of all directives which are part of a linked chain of branching directives.
        /// This is based on but differs slightly from the algorithm for finding getting related directives in
        /// <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.DirectiveTriviaSyntax.GetRelatedDirectives"/>
        /// </summary>
        public static List<DirectiveTriviaSyntax> GetLinkedDirectives(this DirectiveTriviaSyntax directive)
        {
            var list = new List<DirectiveTriviaSyntax>();
            var p = directive.GetPreviousLinkedDirective();
            while (p != null)
            {
                list.Add(p);
                p = p.GetPreviousLinkedDirective();
            }

            list.Reverse();
            list.Add(directive);

            var n = directive.GetNextLinkedDirective();
            while (n != null)
            {
                list.Add(n);
                n = n.GetNextLinkedDirective();
            }

            return list;
        }

        private static DirectiveTriviaSyntax GetPreviousLinkedDirective(this DirectiveTriviaSyntax directive)
        {
            DirectiveTriviaSyntax d = directive.GetPreviousPossiblyLinkedDirective();
            switch (directive.CSharpKind())
            {
                case SyntaxKind.EndIfDirectiveTrivia:
                    while (d != null)
                    {
                        switch (d.CSharpKind())
                        {
                            case SyntaxKind.IfDirectiveTrivia:
                            case SyntaxKind.ElifDirectiveTrivia:
                            case SyntaxKind.ElseDirectiveTrivia:
                                return d;
                        }

                        d = d.GetPreviousPossiblyLinkedDirective();
                    }
                    break;
                case SyntaxKind.ElifDirectiveTrivia:
                    while (d != null)
                    {
                        switch (d.CSharpKind())
                        {
                            case SyntaxKind.IfDirectiveTrivia:
                            case SyntaxKind.ElifDirectiveTrivia:
                                return d;
                        }

                        d = d.GetPreviousPossiblyLinkedDirective();
                    }
                    break;
                case SyntaxKind.ElseDirectiveTrivia:
                    while (d != null)
                    {
                        switch (d.CSharpKind())
                        {
                            case SyntaxKind.IfDirectiveTrivia:
                            case SyntaxKind.ElifDirectiveTrivia:
                                return d;
                        }

                        d = d.GetPreviousPossiblyLinkedDirective();
                    }
                    break;
            }

            return null;
        }

        private static DirectiveTriviaSyntax GetPreviousPossiblyLinkedDirective(this DirectiveTriviaSyntax directive)
        {
            DirectiveTriviaSyntax d = directive;
            while (d != null)
            {
                d = d.GetPreviousDirective();
                if (d != null)
                {
                    // Skip matched sets of directives
                    switch (d.CSharpKind())
                    {
                        case SyntaxKind.EndIfDirectiveTrivia:
                            while (d != null && d.CSharpKind() != SyntaxKind.IfDirectiveTrivia)
                            {
                                d = d.GetPreviousLinkedDirective();
                            }
                            continue;
                    }
                }

                return d;
            }

            return null;
        }

        private static DirectiveTriviaSyntax GetNextLinkedDirective(this DirectiveTriviaSyntax directive)
        {
            DirectiveTriviaSyntax d = directive.GetNextPossiblyLinkedDirective();
            switch (directive.CSharpKind())
            {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                    while (d != null)
                    {
                        switch (d.CSharpKind())
                        {
                            case SyntaxKind.ElifDirectiveTrivia:
                            case SyntaxKind.ElseDirectiveTrivia:
                            case SyntaxKind.EndIfDirectiveTrivia:
                                return d;
                        }

                        d = d.GetNextPossiblyLinkedDirective();
                    }
                    break;
                case SyntaxKind.ElseDirectiveTrivia:
                    while (d != null)
                    {
                        if (d.CSharpKind() == SyntaxKind.EndIfDirectiveTrivia)
                        {
                            return d;
                        }

                        d = d.GetNextPossiblyLinkedDirective();
                    }
                    break;
            }

            return null;
        }
        private static DirectiveTriviaSyntax GetNextPossiblyLinkedDirective(this DirectiveTriviaSyntax directive)
        {
            DirectiveTriviaSyntax d = directive;
            while (d != null)
            {
                d = d.GetNextDirective();
                if (d != null)
                {
                    // Skip matched sets of directives
                    switch (d.CSharpKind())
                    {
                        case SyntaxKind.IfDirectiveTrivia:
                            while (d != null && d.CSharpKind() != SyntaxKind.EndIfDirectiveTrivia)
                            {
                                d = d.GetNextLinkedDirective();
                            }
                            continue;
                    }
                }

                return d;
            }

            return null;
        }
    }
}
