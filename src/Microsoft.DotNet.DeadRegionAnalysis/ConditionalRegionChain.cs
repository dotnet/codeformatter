// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public struct ConditionalRegionChain : IComparable<ConditionalRegionChain>, IEquatable<ConditionalRegionChain>
    {
        private List<ConditionalRegion> _regions;

        public bool IsDefault { get { return _regions == null; } }

        public IReadOnlyList<ConditionalRegion> Regions { get { return _regions; } }

        public int SpanStart { get { return _regions != null ? _regions[0].SpanStart : -1; } }

        public int SpanEnd { get { return _regions != null ? _regions[_regions.Count - 1].SpanEnd : -1; } }

        internal ConditionalRegionChain(List<ConditionalRegion> regions)
        {
            if (regions == null || regions.Count == 0)
            {
                throw new ArgumentException("regions");
            }

            _regions = regions;
        }

        public int CompareTo(ConditionalRegionChain other)
        {
            int result = IsDefault.CompareTo(other.IsDefault);

            if (result == 0)
            {
                result = Regions.Count - other.Regions.Count;
                if (result == 0)
                {
                    result = SpanStart - other.SpanStart;
                    if (result == 0)
                    {
                        return SpanEnd - other.SpanEnd;
                    }
                }
            }

            return result;
        }

        public bool Equals(ConditionalRegionChain other)
        {
            return CompareTo(other) == 0;
        }
    }
}
