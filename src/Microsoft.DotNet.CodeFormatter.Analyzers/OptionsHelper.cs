// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    internal static class OptionsHelper
    {
        private static PropertyBag s_defaultProperties = new PropertyBag();

        private static ConcurrentDictionary<string, PropertyBag> s_additionalFileToOptionsMap = new ConcurrentDictionary<string, PropertyBag>();

        public static PropertyBag GetProperties(AnalyzerOptions analyzerOptions)
        {             
            if (analyzerOptions.AdditionalFiles == null ||  analyzerOptions.AdditionalFiles.Length == 0)
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
    }
}
