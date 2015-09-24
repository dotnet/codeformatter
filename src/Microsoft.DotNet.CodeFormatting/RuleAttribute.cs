// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Composition;

namespace Microsoft.DotNet.CodeFormatting
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SyntaxRule : ExportAttribute, IRuleMetadata
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int Order { get; set; }

        public bool DefaultRule { get; set; }

        public SyntaxRule() : base(typeof(ISyntaxFormattingRule))
        {
            this.Initialize();
        }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class LocalSemanticRule : ExportAttribute, IRuleMetadata
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int Order { get; set; }

        public bool DefaultRule { get; set; }

        public LocalSemanticRule() : base(typeof(ILocalSemanticFormattingRule))
        {
            this.Initialize();
        }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GlobalSemanticRule : ExportAttribute, IRuleMetadata
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int Order { get; set; }

        public bool DefaultRule { get; set; }

        public GlobalSemanticRule() : base(typeof(IGlobalSemanticFormattingRule))
        {
            this.Initialize();
        }
    }

    internal static class IRuleMetadataExtensions
    {
        internal static void Initialize(this IRuleMetadata metadata)
        {
            metadata.Name = String.Empty;
            metadata.Description = String.Empty;
            metadata.Order = int.MaxValue;
            metadata.DefaultRule = true;
        }
    }
}
