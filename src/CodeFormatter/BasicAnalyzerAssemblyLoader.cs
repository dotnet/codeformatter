using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CodeFormatter
{
    public class BasicAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly Dictionary<string, Assembly> _pathsToAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _namesToAssemblies = new Dictionary<string, Assembly>();
        private readonly List<string> _dependencyPaths = new List<string>();

        public void AddDependencyLocation(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            if (!_dependencyPaths.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                _dependencyPaths.Add(fullPath);
            }
        }

        public Assembly LoadFromPath(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            Assembly assembly;
            if (_pathsToAssemblies.TryGetValue(fullPath, out assembly))
            {
                return assembly;
            }
            else
            {
                assembly = Assembly.LoadFrom(fullPath);
                _pathsToAssemblies[fullPath] = assembly;
                _namesToAssemblies[assembly.FullName] = assembly;
                return assembly;
            }
        }
    }
}
