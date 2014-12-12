// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IOrderMetadata
    {
        [DefaultValue(int.MaxValue)]
        int Order { get; }
    }
}
