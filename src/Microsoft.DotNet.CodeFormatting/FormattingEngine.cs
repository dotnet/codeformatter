// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Linq;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition;
using System.Collections.Generic;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        public static IFormattingEngine Create(IEnumerable<string> ruleTypes)
        {
            var catalog = new AssemblyCatalog(typeof(FormattingEngine).Assembly);

            var ruleTypesHash = new HashSet<string>(ruleTypes);
            if (ruleTypesHash.Count == 0)
            {
                ruleTypesHash.Add(RuleTypeConstants.FormatCodeRuleType);
            }

            var filteredCatalog = new FilteredCatalog(catalog, cpd =>
            {
                if (cpd.ExportDefinitions.Any(em =>
                    em.ContractName == AttributedModelServices.GetContractName(typeof(IFormattingRule)) ||
                    em.ContractName == AttributedModelServices.GetContractName(typeof(IFormattingFilter))))
                {
                    object ruleType;
                    if (cpd.Metadata.TryGetValue("RuleType", out ruleType))
                    {
                        if (ruleType is string && !ruleTypesHash.Contains((string)ruleType))
                        {
                            return false;
                        }
                    }
                }

                return true;
            });

            var container = new CompositionContainer(filteredCatalog);
            var engine = container.GetExportedValue<IFormattingEngine>();
            return engine;
        }
    }
}