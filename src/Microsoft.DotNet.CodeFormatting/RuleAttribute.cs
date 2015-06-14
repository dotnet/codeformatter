// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class SyntaxRuleAttribute : ExportAttribute, IRuleMetadata
    {
        public SyntaxRuleAttribute(string name, string description, int order)
            : base(typeof(ISyntaxFormattingRule))
        {
            Name = name;
            Description = description;
            Order = order;
        }

        [DefaultValue("")]
        public string Name { get; private set; }

        [DefaultValue("")]
        public string Description { get; private set; }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }

        [DefaultValue(FormattingLevel.Simple)]
        public FormattingLevel FormattingLevel { get; set; }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class LocalSemanticRuleAttribute : ExportAttribute, IRuleMetadata
    {
        public LocalSemanticRuleAttribute(string name, string description, int order)
            : base(typeof(ILocalSemanticFormattingRule))
        {
            Name = name;
            Description = description;
            Order = order;
        }

        [DefaultValue("")]
        public string Name { get; private set; }

        [DefaultValue("")]
        public string Description { get; private set; }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }

        [DefaultValue(FormattingLevel.Simple)]
        public FormattingLevel FormattingLevel { get; set; }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class GlobalSemanticRuleAttribute : ExportAttribute, IRuleMetadata
    {
        public GlobalSemanticRuleAttribute(string name, string description, int order)
            : base(typeof(IGlobalSemanticFormattingRule))
        {
            Name = name;
            Description = description;
            Order = order;
        }

        [DefaultValue("")]
        public string Name { get; private set; }

        [DefaultValue("")]
        public string Description { get; private set; }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }

        [DefaultValue(FormattingLevel.Simple)]
        public FormattingLevel FormattingLevel { get; set; }
    }
}
