using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public class AnalysisOptions
    {
        public IEnumerable<Project> Projects { get; private set; }

        public IEnumerable<string> ProjectFiles { get; private set; }

        public IEnumerable<string> SourceFiles { get; private set; }

        public IEnumerable<IEnumerable<string>> SymbolConfigurations { get; private set; }

        public IEnumerable<string> AlwaysIgnoredSymbols { get; private set; }

        public IEnumerable<string> AlwaysDefinedSymbols { get; private set; }

        public IEnumerable<string> AlwaysDisabledSymbols { get; private set; }

        public bool Edit { get; private set; }

        public AnalysisOptions(
            IEnumerable<string> files = null,
            IEnumerable<Project> projects = null,
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            bool printEnabled = false,
            bool printDisabled = false,
            bool printVarying = false,
            bool edit = false)
        {
            if (!files.Any())
            {
                throw new ArgumentException("files");
            }

            var firstFileExt = Path.GetExtension(files.First());
            if (firstFileExt.Equals(".csproj", StringComparison.InvariantCultureIgnoreCase))
            {
                ProjectFiles = files;
            }
            else
            {
                SourceFiles = files;
            }

            SymbolConfigurations = symbolConfigurations;
            AlwaysIgnoredSymbols = alwaysIgnoredSymbols;
            AlwaysDefinedSymbols = alwaysDefinedSymbols;
            AlwaysDisabledSymbols = alwaysDisabledSymbols;
        }
    }
}
