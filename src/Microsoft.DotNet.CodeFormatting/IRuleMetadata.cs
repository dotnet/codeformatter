// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
