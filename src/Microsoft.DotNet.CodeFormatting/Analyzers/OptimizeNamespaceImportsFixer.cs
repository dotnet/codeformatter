// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class OptimizeNamespaceImportsFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            var usingDirectiveNodes = new List<SyntaxNode>();

            // We recapitulate the primary diagnostic location in the 
            // Diagnostic.AdditionalLocations property on raising
            // the diagnostic, so this member has complete location details.
            foreach (Location location in diagnostic.AdditionalLocations)
            {
                SyntaxNode usingDirectiveNode = root.FindNode(location.SourceSpan);
                Debug.Assert(usingDirectiveNode != null);
                usingDirectiveNodes.Add(usingDirectiveNode);
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Resources.OptimizeNamespaceImportsFixer_Title,
                    c => RemoveUsingStatement(context.Document, root, usingDirectiveNodes)),
                diagnostic);
        }

        private Task<Document> RemoveUsingStatement(Document document, SyntaxNode root, IEnumerable<SyntaxNode> usingDirectiveNodes)
        {     
            return Task.FromResult(
                document.WithSyntaxRoot(root.RemoveNodes(usingDirectiveNodes, SyntaxRemoveOptions.KeepLeadingTrivia)));
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(OptimizeNamespaceImportsAnalyzer.DiagnosticId);

        ////private static OptimizeNamespaceImportsFixAllProvider s_fixAllProvider = new OptimizeNamespaceImportsFixAllProvider();

        ////private class OptimizeNamespaceImportsFixAllProvider : FixAllProvider
        //{
        //    public override async Task<CodeAction> GetFixAsync(FixAllContext fixAllContext)
        //    {
        //        var diagnosticsToFix = new List<KeyValuePair<Project, ImmutableArray<Diagnostic>>>();
        //        string titleFormat = "Remove all unnecessary using directives in {0} {1}";
        //        string title = null;

        //        switch (fixAllContext.Scope)
        //        {
        //            case FixAllScope.Document:
        //                {
        //                    var diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false);
        //                    diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(fixAllContext.Project, diagnostics));
        //                    title = string.Format(titleFormat, "document", fixAllContext.Document.Name);
        //                    break;
        //                }

        //            case FixAllScope.Project:
        //                {
        //                    var project = fixAllContext.Project;
        //                    ImmutableArray<Diagnostic> diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
        //                    diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(fixAllContext.Project, diagnostics));
        //                    title = string.Format(titleFormat, "project", fixAllContext.Project.Name);
        //                    break;
        //                }

        //            case FixAllScope.Solution:
        //                {
        //                    foreach (var project in fixAllContext.Solution.Projects)
        //                    {
        //                        ImmutableArray<Diagnostic> diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
        //                        diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(project, diagnostics));
        //                    }

        //                    title = "Add all items in the solution to the public API";
        //                    break;
        //                }

        //            case FixAllScope.Custom:
        //                return null;

        //            default:
        //                break;
        //        }

        //        return new FixAllAdditionalDocumentChangeAction(title, fixAllContext.Solution, diagnosticsToFix);
        //    }
        //}
    }
}
