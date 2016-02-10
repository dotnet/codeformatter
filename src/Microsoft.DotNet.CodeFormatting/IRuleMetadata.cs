// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IRuleMetadata
    {
        string Name { get; set; }

        string Description { get; set; }

        int Order { get; set; }

        bool DefaultRule { get; set; }
    }
}
