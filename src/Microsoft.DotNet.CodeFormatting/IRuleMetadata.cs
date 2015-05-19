﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IRuleMetadata
    {
        [DefaultValue(int.MaxValue)]
        int Order { get; }

        [DefaultValue(FormattingLevel.Simple)]
        FormattingLevel FormattingLevel { get; }

        [DefaultValue(false)]
        bool Skip { get; }
    }
}
