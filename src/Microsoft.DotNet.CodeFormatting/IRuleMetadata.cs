// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IRuleMetadata
    {
        [DefaultValue("")]
        string Name { get; }

        [DefaultValue("")]
        string Description { get; }

        [DefaultValue(int.MaxValue)]
        int Order { get; }

        [DefaultValue(true)]
        bool DefaultRule { get; }
    }
}
