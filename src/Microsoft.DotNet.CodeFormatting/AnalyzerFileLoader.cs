using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.CodeFormatting
{
    public class AnalyzerFileLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath)
        {
            return Assembly.LoadFrom(fullPath);
        }
    }
}
