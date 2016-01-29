// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRule(HasNoIllegalHeadersFormattingRule.Name, HasNoIllegalHeadersFormattingRule.Description, LocalSemanticRuleOrder.HasNoIllegalHeadersFormattingRule)]
    internal sealed class HasNoIllegalHeadersFormattingRule : CSharpOnlyFormattingRule, ILocalSemanticFormattingRule
    {
        internal const string Name = "IllegalHeaders";
        internal const string Description = "Remove illegal headers from files";

        // We are going to replace this header with the actual filename of the document being processed
        private const string FileNameIllegalHeader = "<<<filename>>>";

        // We are going to remove any multiline comments that *only* contain these characters
        private const string CommentFormattingCharacters = "*/=-";

        public Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var leadingTrivia = syntaxNode.GetLeadingTrivia();
            SyntaxTriviaList newTrivia = leadingTrivia;
            var illegalHeaders = GetIllegalHeaders(document);

            // We also want to add the filename (without path but with extension) to this list.

            // because we are mutating the list, once we remove a header, we won't remove any others...
            for (int idx = 0; idx < illegalHeaders.Length; idx++)
            {
                var illegalHeader = illegalHeaders[idx];
                foreach (var trivia in newTrivia)
                {
                    // If we have an illegal header here...
                    if (trivia.ToFullString().IndexOf(illegalHeader, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                        {
                            // For multiline comment trivia we need to process them line by line and remove all the illegal headers.
                            // We then need to re-create the multiline comment and append it to the list.
                            var modifiedTrivia = RemoveIllegalHeadersFromMultilineComment(newTrivia, trivia, illegalHeader);

                            // We need to go back and re-try the current illegal header if we have modified the multiline trivia.
                            if (modifiedTrivia != newTrivia)
                            {
                                newTrivia = modifiedTrivia;
                                idx--;
                            }
                            break;
                        }
                        else
                        {
                            var index = newTrivia.IndexOf(trivia);

                            newTrivia = RemoveTriviaAtIndex(newTrivia, index);

                            // We need to re-try the current illegal header to make sure there are no other comments containing it
                            // further down the trivia list
                            idx--;
                            break;
                        }
                    }
                }
            }

            if (leadingTrivia.Equals(newTrivia))
                return Task.FromResult(syntaxNode);

            return Task.FromResult(syntaxNode.WithLeadingTrivia(newTrivia));
        }

        private string[] GetIllegalHeaders(Document document)
        {
            var filePath = Path.Combine(
                Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path)),
                "IllegalHeaders.md");

            var illegalHeaders = new HashSet<string>(File.ReadAllLines(filePath).Where(l => !l.StartsWith("##") && !l.Equals("")), StringComparer.OrdinalIgnoreCase);

            // Generate the dynamic header (if applicable)
            if (illegalHeaders.Contains(FileNameIllegalHeader))
            {
                illegalHeaders.Remove(FileNameIllegalHeader);
                illegalHeaders.Add(document.Name);
            }

            return illegalHeaders.ToArray();
        }

        private SyntaxTriviaList RemoveIllegalHeadersFromMultilineComment(SyntaxTriviaList newTrivia, SyntaxTrivia trivia, string illegalHeader)
        {
            StringBuilder newTriviaString = new StringBuilder();
            bool commentHasMeaningfulInfo = false;
            bool removedIllegalHeaders = false;
            using (StringReader sr = new StringReader(trivia.ToFullString()))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    // If the current line contains the illegal header
                    if (line.IndexOf(illegalHeader, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // special care must be had to keep the /* and */ tokens.
                        if (line.TrimStart().StartsWith("/*"))
                        {
                            // Note: This will also cover the case where the comment is: /* illegalHeader */ as we remove the entire line (including the */).
                            newTriviaString.AppendLine("/*");
                        }
                        else if (line.TrimEnd().EndsWith("*/"))
                        {
                            newTriviaString.AppendLine("*/");
                        }
                        removedIllegalHeaders = true;
                    }
                    else
                    {
                        commentHasMeaningfulInfo |= CommentLineContainsMeaningfulIInformation(line);

                        newTriviaString.AppendLine(line);
                    }
                }
            }

            // We should not remove any comments if we don't have to.
            if (!removedIllegalHeaders)
            {
                return newTrivia;
            }

            // Remove the old trivia and replace it with the new trivia
            var index = newTrivia.IndexOf(trivia);
            newTrivia = RemoveTriviaAtIndex(newTrivia, index);

            if (commentHasMeaningfulInfo)
            {
                // we need to remove the original multiline comment and replace it with this new one.
                var newMultilineComment = SyntaxFactory.Comment(newTriviaString.ToString());
                newTrivia = newTrivia.Insert(index, newMultilineComment);
            }

            return newTrivia;
        }

        private static bool CommentLineContainsMeaningfulIInformation(string line)
        {
            // We are going to assume that any comments that only contain:
            // *, / , =, - are safe to remove.
            string newLine = line;
            for (int i = 0; i < CommentFormattingCharacters.Length; i++)
            {
                newLine = newLine.Replace(CommentFormattingCharacters[i], ' ');
            }
            if (newLine.Trim() == string.Empty)
                return false;

            return true;
        }

        private static SyntaxTriviaList RemoveTriviaAtIndex(SyntaxTriviaList newTrivia, int index)
        {
            // Remove trivia
            newTrivia = newTrivia.RemoveAt(index);

            // Remove end of line after trivia
            if (index < newTrivia.Count && newTrivia.ElementAt(index).Kind() == SyntaxKind.EndOfLineTrivia)
            {
                newTrivia = newTrivia.RemoveAt(index);
            }

            return newTrivia;
        }
    }
}
