// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Composition;

namespace Microsoft.DotNet.CodeFormatting
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SyntaxRule : ExportAttribute, IRuleMetadata
    {
        [DefaultValue("")]
        public string Name { get; set; }

        [DefaultValue("")]
        public string Description { get; set; }

        [DefaultValue(int.MaxValue)]
        public int Order { get; set; }

        [DefaultValue(true)]
        public bool DefaultRule { get; set; }

        public SyntaxRule() : base(typeof(ISyntaxFormattingRule))
        {
        }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class LocalSemanticRule : ExportAttribute, IRuleMetadata
    {
        [DefaultValue("")]
        public string Name { get; set; }

        [DefaultValue("")]
        public string Description { get; set; }

        [DefaultValue(int.MaxValue)]
        public int Order { get; set; }

        [DefaultValue(true)]
        public bool DefaultRule { get; set; }

        public LocalSemanticRule() : base(typeof(ILocalSemanticFormattingRule))
        {
        }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GlobalSemanticRule : ExportAttribute, IRuleMetadata
    {
        [DefaultValue("")]
        public string Name { get; set; }

        [DefaultValue("")]
        public string Description { get; set; }

        [DefaultValue(int.MaxValue)]
        public int Order { get; set; }

        [DefaultValue(true)]
        public bool DefaultRule { get; set; }

        public GlobalSemanticRule() : base(typeof(IGlobalSemanticFormattingRule))
        {
        }
    }
}
