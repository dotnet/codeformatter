using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    internal static class AnalyzerIds
    {
        public const string ExplicitThis             = "DNS1000";
        public const string UnwrittenWritableField   = "DNS1001";
        public const string OrderModifiers           = "DNS1002";
        public const string OptimizeNamespaceImports = "DNS1003";
    }
}
