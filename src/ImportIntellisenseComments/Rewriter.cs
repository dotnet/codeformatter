using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

namespace ImportIntellisenseComments
{
    class Rewriter : CSharpSyntaxRewriter
    {
        private SemanticModel m_model;
        private Dictionary<string, string> _membersDictionary;

        public Rewriter(SemanticModel model, Dictionary<string, string> membersDictionary)
        {
            m_model = model;
            _membersDictionary = membersDictionary;
        }

        /// <summary>
        /// Given a SyntaxNode for an API node, look up the doc comment and return it as a string.
        /// </summary>
        /// <param name="id">CommentID for the API</param>
        /// <returns>/// comments for the given API</returns>
        public string GetDocCommentForId(string id)
        {
            string docComment = string.Empty;
            try
            {
                string intelliSenseContent = _membersDictionary[id];

                intelliSenseContent = intelliSenseContent.Replace("\n", "");

                using (XmlReader reader = XmlReader.Create(new StringReader(intelliSenseContent)))
                {
                    StringBuilder output = new StringBuilder();
                    reader.ReadToDescendant("summary");
                    do
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                            output.Append("/// " + reader.ReadOuterXml() + "\r\n");
                    } while (reader.Read());
                    docComment = output.ToString().Replace("        ", "");
                }
            }
            catch(KeyNotFoundException)
            {
#if DEBUG
                // Writing the IDs not found to investigate possible issues with the tool
                System.Diagnostics.Debug.WriteLine($"id not found {id}");
#endif
            }

            return docComment;

        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (ClassDeclarationSyntax) ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (MethodDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (ConstructorDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (DelegateDeclarationSyntax)base.VisitDelegateDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (DelegateDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (ConversionOperatorDeclarationSyntax) base.VisitConversionOperatorDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (ConversionOperatorDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (DestructorDeclarationSyntax)base.VisitDestructorDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (DestructorDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (EnumDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (EventDeclarationSyntax)base.VisitEventDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (EventDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            if (node == null)
                return null;
            //var symbol = m_model.GetDeclaredSymbol(node);
            var symbol = m_model.GetDeclaredSymbol(node.Declaration.Variables.First());
            node = (EventFieldDeclarationSyntax)base.VisitEventFieldDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (EventFieldDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node.Declaration.Variables.First());
            node = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node);            
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (FieldDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (IndexerDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (InterfaceDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (OperatorDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (PropertyDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (StructDeclarationSyntax)base.VisitStructDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (StructDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        public override SyntaxNode VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node) 
        {
            if (node == null)
                return null;
            var symbol = m_model.GetDeclaredSymbol(node);
            node = (EnumMemberDeclarationSyntax)base.VisitEnumMemberDeclaration(node);
            if (!IsPrivateOrInternal(symbol.DeclaredAccessibility))
                node = (EnumMemberDeclarationSyntax)ApplyDocComment(node, symbol.GetDocumentationCommentId());
            return node;
        }

        private SyntaxNode ApplyDocComment(SyntaxNode node, string docCommentId)
        {
            if (docCommentId == null)
                return node;

            // Look up the comment text
            string docCommentText = GetDocCommentForId(docCommentId);

            // Get the SyntaxTrivia for the comment
            SyntaxTree newTree = (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(docCommentText);
            var newTrivia = newTree.GetRoot().GetLeadingTrivia();

            if (node.HasLeadingTrivia)
            {
                SyntaxTriviaList triviaList = node.GetLeadingTrivia();
                SyntaxTrivia firstComment = triviaList.Last();

                // Check to see if there are any existing doc comments
                var docComments = triviaList
                        .Where(n => n.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || n.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                        .Select(t => t.GetStructure())
                        .OfType<DocumentationCommentTriviaSyntax>()
                        .ToList();

                if (!docComments.Any())
                {
                    // Append the doc comment
                    node = node.InsertTriviaBefore(firstComment, newTrivia);
                }
#if DEBUG
                else
                {
                    System.Diagnostics.Debug.WriteLine($"/// comment already exists: {docCommentId}");
                }
#endif
            }
            else // no leading trivia
            {
                node = node.WithLeadingTrivia(newTrivia);
            }
            return node;
        }

        private bool IsPrivateOrInternal(Accessibility enumValue)
        {
            return new[] { Accessibility.Private, Accessibility.Internal }.Contains(enumValue);
        }

    }
}