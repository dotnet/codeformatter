// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Rename;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [GlobalSemanticRule(PrivateFieldNamingRule.Name, PrivateFieldNamingRule.Description, GlobalSemanticRuleOrder.PrivateFieldNamingRule)]
    internal partial class PrivateFieldNamingRule : IGlobalSemanticFormattingRule
    {
        internal const string Name = "FieldNames";
        internal const string Description = "Prefix private fields with _ and statics with s_";

        #region CommonRule

        private abstract class CommonRule
        {
            protected abstract SyntaxNode AddPrivateFieldAnnotations(SyntaxNode syntaxNode, out int count);

            /// <summary>
            /// This method exists to work around DevDiv 1086632 in Roslyn.  The Rename action is 
            /// leaving a set of annotations in the tree.  These annotations slow down further processing
            /// and eventually make the rename operation unusable.  As a temporary work around we manually
            /// remove these from the tree.
            /// </summary>
            protected abstract SyntaxNode RemoveRenameAnnotations(SyntaxNode syntaxNode);

            public async Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken)
            {
                int count;
                var newSyntaxRoot = AddPrivateFieldAnnotations(syntaxRoot, out count);

                if (count == 0)
                {
                    return document.Project.Solution;
                }

                var documentId = document.Id;
                var solution = document.Project.Solution;
                solution = solution.WithDocumentSyntaxRoot(documentId, newSyntaxRoot);
                solution = await RenameFields(solution, documentId, count, cancellationToken);
                return solution;
            }

            private async Task<Solution> RenameFields(Solution solution, DocumentId documentId, int count, CancellationToken cancellationToken)
            {
                Solution oldSolution = null;
                for (int i = 0; i < count; i++)
                {
                    oldSolution = solution;

                    var semanticModel = await solution.GetDocument(documentId).GetSemanticModelAsync(cancellationToken);
                    var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
                    var declaration = root.GetAnnotatedNodes(s_markerAnnotation).ElementAt(i);

                    // Make note, VB represents "fields" marked as "WithEvents" as properties, so don't be
                    // tempted to treat this as a IFieldSymbol. We only need the name, so ISymbol is enough.
                    var fieldSymbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
                    var newName = GetNewFieldName(fieldSymbol);

                    // Can happen with pathologically bad field names like _
                    if (newName == fieldSymbol.Name)
                    {
                        continue;
                    }

                    solution = await Renamer.RenameSymbolAsync(solution, fieldSymbol, newName, solution.Workspace.Options, cancellationToken).ConfigureAwait(false);
                    solution = await CleanSolutionAsync(solution, oldSolution, cancellationToken);
                }

                return solution;
            }

            private static string GetNewFieldName(ISymbol fieldSymbol)
            {
                var name = fieldSymbol.Name.Trim('_');
                if (name.Length > 2 && char.IsLetter(name[0]) && name[1] == '_')
                {
                    name = name.Substring(2);
                }

                // Some .NET code uses "ts_" prefix for thread static
                if (name.Length > 3 && name.StartsWith("ts_", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(3);
                }

                if (name.Length == 0)
                {
                    return fieldSymbol.Name;
                }

                if (name.Length > 2 && char.IsUpper(name[0]) && char.IsLower(name[1]))
                {
                    name = char.ToLower(name[0]) + name.Substring(1);
                }

                if (fieldSymbol.IsStatic)
                {
                    // Check for ThreadStatic private fields.
                    if (fieldSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals("ThreadStaticAttribute", StringComparison.Ordinal)))
                    {
                        return "t_" + name;
                    }
                    else
                    {
                        return "s_" + name;
                    }
                }

                return "_" + name;
            }

            private async Task<Solution> CleanSolutionAsync(Solution newSolution, Solution oldSolution, CancellationToken cancellationToken)
            {
                var solution = newSolution;

                foreach (var projectChange in newSolution.GetChanges(oldSolution).GetProjectChanges())
                {
                    foreach (var documentId in projectChange.GetChangedDocuments())
                    {
                        solution = await CleanSolutionDocument(solution, documentId, cancellationToken);
                    }
                }

                return solution;
            }

            private async Task<Solution> CleanSolutionDocument(Solution solution, DocumentId documentId, CancellationToken cancellationToken)
            {
                var document = solution.GetDocument(documentId);
                var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken);
                if (syntaxNode == null)
                {
                    return solution;
                }

                var newNode = RemoveRenameAnnotations(syntaxNode);
                return solution.WithDocumentSyntaxRoot(documentId, newNode);
            }
        }

        #endregion

        private const string s_renameAnnotationName = "Rename";

        private readonly static SyntaxAnnotation s_markerAnnotation = new SyntaxAnnotation("PrivateFieldToRename");

        // Used to avoid the array allocation on calls to WithAdditionalAnnotations
        private readonly static SyntaxAnnotation[] s_markerAnnotationArray;

        static PrivateFieldNamingRule()
        {
            s_markerAnnotationArray = new[] { s_markerAnnotation };
        }

        private readonly CSharpRule _csharpRule = new CSharpRule();
        private readonly VisualBasicRule _visualBasicRule = new VisualBasicRule();

        public bool SupportsLanguage(string languageName)
        {
            return
                languageName == LanguageNames.CSharp ||
                languageName == LanguageNames.VisualBasic;
        }

        public Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxRoot, CancellationToken cancellationToken)
        {
            switch (document.Project.Language)
            {
                case LanguageNames.CSharp:
                    return _csharpRule.ProcessAsync(document, syntaxRoot, cancellationToken);
                case LanguageNames.VisualBasic:
                    return _visualBasicRule.ProcessAsync(document, syntaxRoot, cancellationToken);
                default:
                    throw new NotSupportedException();
            }
        }

        private static bool IsGoodPrivateFieldName(string name, bool isInstance)
        {
            if (isInstance)
            {
                return name.Length > 0 && name[0] == '_';
            }
            else
            {
                return name.Length > 1 && (name[0] == 's' || name[0] == 't') && name[1] == '_';
            }
        }
    }
}
