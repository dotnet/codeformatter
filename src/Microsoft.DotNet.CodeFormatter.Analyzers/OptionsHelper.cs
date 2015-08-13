// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    internal static class OptionsHelper
    {
        private static PropertyBag defaultProperties = new PropertyBag();

        private static ConcurrentDictionary<string, PropertyBag> additionalFileToOptionsMap = new ConcurrentDictionary<string, PropertyBag>();

        public static PropertyBag GetProperties(AnalyzerOptions analyzerOptions)
        {             
            if (analyzerOptions.AdditionalFiles == null ||  analyzerOptions.AdditionalFiles.Length == 0)
            {
                return defaultProperties;
            }

            Debug.Assert(analyzerOptions.AdditionalFiles.Length == 1);

            string optionsFilePath = analyzerOptions.AdditionalFiles[0].Path;

            PropertyBag properties;

            if (!additionalFileToOptionsMap.TryGetValue(optionsFilePath, out properties))
            {
                properties = new PropertyBag();
                properties.LoadFrom(optionsFilePath);
                properties = additionalFileToOptionsMap.GetOrAdd(optionsFilePath, properties);
            }
            return properties;
        }
    }
}
