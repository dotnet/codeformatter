// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    // TODO: does this need to be async? 
    internal interface IFormattingFilter
    {
        Task<bool> ShouldBeProcessedAsync(Document document);
    }
}