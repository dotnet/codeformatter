using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using EditorConfig.Core;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    class ConfiguredAdditionalDocument : IDisposable
    {
        Lazy<TextDocument> document;
        Lazy<FileConfiguration> configuration;

        public ConfiguredAdditionalDocument(Solution solution, DocumentId documentId, IEditorConfigProvider configProvider)
        {
            document = new Lazy<TextDocument>(() => solution.GetAdditionalDocument(documentId));
            configuration = new Lazy<FileConfiguration>(() => configProvider.GetConfiguration(Value));
        }

        public TextDocument Value => document.Value;

        public FileConfiguration Configuration => configuration.Value;

        public void Dispose()
        {
        }
    }
}
