using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using EditorConfig.Core;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [TextDocumentRule(Name, Description, TextDocumentRuleOrder.FormatXmlDocumentRule)]
    class FormatXmlDocumentRule : ITextDocumentFormattingRule
    {
        internal const string Name = "FormatXmlDocument";
        internal const string Description = "Format XML-based additional documents";

        public async Task<Solution> ProcessAsync(TextDocument document, FileConfiguration config, CancellationToken cancellationToken)
        {
            var sourceText = await document.GetTextAsync(cancellationToken);

            var xmlDocument = default(XDocument);

            try
            {
                // Because of changes to additional files are not allowed by the MsBuildWorkspace
                // we need to read the file from disk to avoid getting an invalid text that
                // was already modified by another rule
                using (var reader = new StreamReader(document.FilePath))
                    xmlDocument = XDocument.Load(reader);
            }
            catch
            {
                // something went wrong we could not read the text document (or it was not an Xml document) }
                return document.Project.Solution;
            }

            var xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = "  ";

            if (config != null)
            {
                if (config.IndentStyle.HasValue && config.IndentStyle.Value == IndentStyle.Tab)
                    xmlWriterSettings.IndentChars = "\t";
                else if (config.IndentSize != null && config.IndentSize.NumberOfColumns.HasValue)
                    xmlWriterSettings.IndentChars = new string(' ', config.IndentSize.NumberOfColumns.Value);
            }

            // Regrettably the MsBuildWorkspace does not support ApplyChnagesKind.ChangeAdditionalDocument
            // So we need to write the content explicitly
            using (var writer = XmlWriter.Create(
                    new StreamWriter(document.FilePath, false, sourceText.Encoding), xmlWriterSettings))
            {
                xmlDocument.WriteTo(writer);
            }

            // Otherwise we could replate the AdditionalDocument text with this:
            //var newText = sourceText.Replace(0, sourceText.Length, formattedXml);
            //return document.Project.Solution.WithAdditionalDocumentText(document.Id, newText);

            return document.Project.Solution;
        }

        public bool SupportsLanguage(string languageName) => true;
    }
}
