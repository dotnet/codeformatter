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
    public sealed class RuleOrderAttribute : ExportAttribute
    {
        public RuleOrderAttribute(int order)
            : base(typeof(IFormattingRule))
        {
            Order = order;
        }

        [DefaultValue(int.MaxValue)]
        public int Order { get; private set; }
    }
}
