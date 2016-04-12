// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.DotNet.CodeFormatting;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    internal static class OptionsHelper
    {
        public static Assembly[] DefaultCompositionAssemblies =
                                        new Assembly[] {
                                            typeof(FormattingEngine).Assembly,
                                            typeof(OptimizeNamespaceImportsAnalyzer).Assembly
                                        };


        private static PropertyBag s_defaultProperties = new PropertyBag();

        private static ConcurrentDictionary<string, PropertyBag> s_additionalFileToOptionsMap = new ConcurrentDictionary<string, PropertyBag>();

        internal delegate PropertyBag GetPropertiesCallback(AnalyzerOptions analyzerOptions);

        internal static GetPropertiesCallback GetPropertiesImplementation = GetPropertiesDefault;

        public static PropertyBag GetProperties(AnalyzerOptions analyzerOptions)
        {
            return GetPropertiesImplementation(analyzerOptions);
        }

        private static PropertyBag GetPropertiesDefault(AnalyzerOptions analyzerOptions)
        {
            if (analyzerOptions.AdditionalFiles == null || analyzerOptions.AdditionalFiles.Length == 0)
            {
                return s_defaultProperties;
            }

            PropertyBag properties = null;

            foreach (AdditionalText additionalText in analyzerOptions.AdditionalFiles)
            {
                string additionalFilePath = additionalText.Path;

                if (!Path.GetExtension(additionalFilePath).Equals(".formatconfig", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (properties != null)
                {
                    throw new InvalidOperationException("Multiple formatting configuration files were specified as analyzer options.");
                }

                if (!s_additionalFileToOptionsMap.TryGetValue(additionalFilePath, out properties))
                {
                    properties = new PropertyBag();
                    properties.LoadFrom(additionalFilePath);
                    properties = s_additionalFileToOptionsMap.GetOrAdd(additionalFilePath, properties);
                }
            }

            return properties ?? s_defaultProperties;
        }

        internal static PropertyBag BuildDefaultPropertyBag()
        {
            PropertyBag allOptions = new PropertyBag();

            // The export command could be updated in the future to accept an arbitrary set
            // of analyzers for which to build an options XML file suitable for configuring them.
            // Currently, we perform discovery against the built-in CodeFormatter rules
            // and analyzers only.
            foreach (IOptionsProvider provider in FormattingEngine.GetOptionsProviders(DefaultCompositionAssemblies))
            {
                foreach (IOption option in provider.GetOptions())
                {
                    allOptions.SetProperty(option, option.DefaultValue);
                }
            }

            // TODO: this needs to be fixed. Instead of maintaining a hard-coded list of analyzers, that needs to 
            //       be updated on adding a new check, we need to expose the analyzer name on an appropriate interface.
            //       Retrieving each of these bools populates the property bag with the default value of 'true'
            bool enabled;
            
            foreach (string analyzerName in AllAnalyzerNames)
            {
                enabled = allOptions.GetProperty(BuildDefaultEnabledProperty(analyzerName));
            }
            return allOptions;
        }
        public static PerLanguageOption<bool> BuildDefaultEnabledProperty(string analyzerName)
        {
            return new PerLanguageOption<bool>(analyzerName, "Enabled", defaultValue: true);
        }

        // TODO: fix hardcoded list
        internal static IEnumerable<string> AllAnalyzerNames = new string[] {
            ExplicitThisAnalyzer.AnalyzerName,
            OptimizeNamespaceImportsAnalyzer.AnalyzerName,
            ProvideExplicitVariableTypeAnalyzer.AnalyzerName, 
            UnwrittenWritableFieldAnalyzer.AnalyzerName
        };
    }
}
