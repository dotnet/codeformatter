using System;
using System.Dynamic;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EditorConfig.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using System.Collections.ObjectModel;

namespace Microsoft.DotNet.CodeFormatting
{
    class ConfiguredDocument : IDisposable
    {
        Lazy<Document> document;

        OptionSet optionsToBeRestored;
        Workspace workspace;

        public ConfiguredDocument(Solution solution, DocumentId documentId, IEditorConfigProvider configProvider)
        {
            workspace = solution.Workspace;

            document = new Lazy<Document>(() =>
            {
                var result = solution.GetDocument(documentId);
                var config = configProvider.GetConfiguration(result);
                if (config != null)
                {
                    optionsToBeRestored = workspace.Options;
                    ConfigureWorkspaceForDocument(result, config);
                }

                return result;
            });
        }

        public Document Value => document.Value;

        public void Dispose()
        {
            if (optionsToBeRestored != null)
            {
                workspace.Options = optionsToBeRestored;
                optionsToBeRestored = null;
                workspace = null;
            }
        }

        void ConfigureWorkspaceForDocument(Document document, FileConfiguration config)
        {
            // Microsoft.CodeAnalysis.Options.IOptionService
            var optionService = workspace
                .AsDynamicReflection()
                ._workspaceOptionService;

            // Get the registered options in the roslyn workspace
            IEnumerable<IOption> registeredOptions = optionService.GetRegisteredOptions();

            foreach (var option in registeredOptions.Where(x => x.StorageLocations != null))
            {
                // Get the EditorConfig storage of the option
                OptionStorageLocation editorConfigStorageLocation = option
                    .StorageLocations
                    .FirstOrDefault(x => x.GetType().Name == "EditorConfigStorageLocation`1");

                // If it's null, it means that the option in the workspace does not have a corresponding storage in the .editorconfig file.
                if (editorConfigStorageLocation != null)
                {
                    string editorConfigKey = editorConfigStorageLocation.AsDynamicReflection().KeyName;

                    // Get the value in the .editorconfig associated with the editorConfig storage key
                    if (config.Properties.TryGetValue(editorConfigKey, out var editorConfigValue))
                    {
                        // Map the value in the .editorconfig file to the Option value in the roslyn workspace
                        // by invoking Microsoft.CodeAnalysis.Options.EditorConfigStorageLocation<T>.TryOption(...) 
                        object optionValue = default(object);
                        if (editorConfigStorageLocation.AsDynamicReflection().TryGetOption(
                                option,
                                new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                                {
                                        { editorConfigKey, editorConfigValue }
                                }),
                                option.Type,
                                OutValue.Create<object>(x => optionValue = x)))
                        {
                            var optionKey = new OptionKey(
                                option,
                                option.IsPerLanguage ? document.Project.Language : null);

                            workspace.Options = workspace.Options.WithChangedOption(optionKey, optionValue);
                        }
                    }
                }
            }
        }
    }
}
