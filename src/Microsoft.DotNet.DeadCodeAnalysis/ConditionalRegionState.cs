using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public enum ConditionalRegionState
    {
        AlwaysDisabled = 0,
        AlwaysEnabled = 1,
        Varying = 2
    }
}
