// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under MIT. See LICENSE in the project root for license information.
using System;
using System.Linq;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using Microsoft.DotNet.CodeFormatting.Rules;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        private const string RuleTypeNotFoundError = "The specified rule type was not found: ";

        public static IFormattingEngine Create(IEnumerable<string> ruleTypes)
        {
            var catalog = new AssemblyCatalog(typeof(FormattingEngine).Assembly);

            var ruleTypesHash = new HashSet<string>(ruleTypes, StringComparer.InvariantCultureIgnoreCase);
            var notFoundRuleTypes = new HashSet<string>(ruleTypes, StringComparer.InvariantCultureIgnoreCase);

            var filteredCatalog = new FilteredCatalog(catalog, cpd =>
            {
                if (cpd.ExportDefinitions.Any(em =>
                    em.ContractName == AttributedModelServices.GetContractName(typeof(IFormattingRule)) ||
                    em.ContractName == AttributedModelServices.GetContractName(typeof(IFormattingFilter))))
                {
                    object ruleType;
                    if (cpd.Metadata.TryGetValue(RuleTypeConstants.PartMetadataKey, out ruleType))
                    {
                        if (ruleType is string)
                        {
                            notFoundRuleTypes.Remove((string)ruleType);
                            if (!ruleTypesHash.Contains((string)ruleType))
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            });

            var container = new CompositionContainer(filteredCatalog);
            var engine = container.GetExportedValue<IFormattingEngine>();

            //  Need to do this after the catalog is queried, otherwise the lambda won't have been run
            foreach (var notFoundRuleType in notFoundRuleTypes)
            {
                (RuleTypeNotFoundError + notFoundRuleType).WriteConsoleError(1, "");
            }

            return engine;
        }
    }
}