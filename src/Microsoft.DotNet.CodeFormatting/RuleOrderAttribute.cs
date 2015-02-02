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
    internal sealed class SyntaxRuleOrderAttribute : ExportAttribute
    {
        public SyntaxRuleOrderAttribute(int order)
            : base(typeof(ISyntaxFormattingRule))
        {
            Order = order;
        }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class LocalSemanticRuleOrderAttribute : ExportAttribute
    {
        public LocalSemanticRuleOrderAttribute(int order)
            : base(typeof(ILocalSemanticFormattingRule))
        {
            Order = order;
        }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class GlobalSemanticRuleOrderAttribute : ExportAttribute
    {
        public GlobalSemanticRuleOrderAttribute(int order)
            : base(typeof(IGlobalSemanticFormattingRule))
        {
            Order = order;
        }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }
    }
}
