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
        public IEnumerable<string> ProjectPaths { get; private set; }

        public IEnumerable<string> SourcePaths { get; private set; }

        public IEnumerable<string> Sources { get; private set; }

        public IEnumerable<IEnumerable<string>> SymbolConfigurations { get; private set; }

        public IEnumerable<string> AlwaysIgnoredSymbols { get; private set; }

        public IEnumerable<string> AlwaysDefinedSymbols { get; private set; }

        public IEnumerable<string> AlwaysDisabledSymbols { get; private set; }

        public bool PrintEnabled { get; private set; }

        public bool PrintDisabled { get; private set; }

        public bool PrintVarying { get; private set; }

        public bool Edit { get; private set; }

        public static AnalysisOptions FromFilePaths(
            IEnumerable<string> filePaths,
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            bool printEnabled = false,
            bool printDisabled = false,
            bool printVarying = false,
            bool edit = false)
        {
            if (filePaths == null || !filePaths.Any())
            {
                throw new ArgumentException("Must specify at least one file path");
            }

            IEnumerable<string> projectPaths = null;
            IEnumerable<string> sourcePaths = null;

            var firstFileExt = Path.GetExtension(filePaths.First());
            if (firstFileExt.Equals(".csproj", StringComparison.InvariantCultureIgnoreCase))
            {
                projectPaths = filePaths;
            }
            else
            {
                sourcePaths = filePaths;
            }

            return new AnalysisOptions(
                projectPaths: projectPaths,
                sourcePaths: sourcePaths,
                sources: null,
                symbolConfigurations: symbolConfigurations,
                alwaysIgnoredSymbols: alwaysIgnoredSymbols,
                alwaysDefinedSymbols: alwaysDefinedSymbols,
                alwaysDisabledSymbols: alwaysDisabledSymbols,
                printEnabled: printEnabled,
                printDisabled: printDisabled,
                printVarying: printVarying,
                edit: edit);
        }

        public static AnalysisOptions FromSources(
            IEnumerable<string> sources,
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            bool printEnabled = false,
            bool printDisabled = false,
            bool printVarying = false,
            bool edit = false)
        {
            if (sources != null && !sources.Any())
            {
                throw new ArgumentException("Must specify at least one source text");
            }

            return new AnalysisOptions(
                projectPaths: null,
                sourcePaths: null,
                sources: sources,
                symbolConfigurations: symbolConfigurations,
                alwaysIgnoredSymbols: alwaysIgnoredSymbols,
                alwaysDefinedSymbols: alwaysDefinedSymbols,
                alwaysDisabledSymbols: alwaysDisabledSymbols,
                printEnabled: printEnabled,
                printDisabled: printDisabled,
                printVarying: printVarying,
                edit: edit);
        }

        private AnalysisOptions(
            IEnumerable<string> projectPaths = null,
            IEnumerable<string> sourcePaths = null,
            IEnumerable<string> sources = null,
            IEnumerable<IEnumerable<string>> symbolConfigurations = null,
            IEnumerable<string> alwaysIgnoredSymbols = null,
            IEnumerable<string> alwaysDefinedSymbols = null,
            IEnumerable<string> alwaysDisabledSymbols = null,
            bool printEnabled = false,
            bool printDisabled = false,
            bool printVarying = false,
            bool edit = false)
        {
            ProjectPaths = projectPaths;
            SourcePaths = sourcePaths;
            Sources = sources;

            SymbolConfigurations = symbolConfigurations != null ?
                symbolConfigurations.Select(c => RemoveDuplicates(c)) :
                null;

            AlwaysIgnoredSymbols = RemoveDuplicates(alwaysIgnoredSymbols);
            AlwaysDefinedSymbols = RemoveDuplicates(alwaysDefinedSymbols);
            AlwaysDisabledSymbols = RemoveDuplicates(alwaysDisabledSymbols);
            PrintEnabled = printEnabled;
            PrintDisabled = printDisabled;
            PrintVarying = printVarying;
            Edit = edit;
        }

        private static IEnumerable<string> RemoveDuplicates(IEnumerable<string> input)
        {
            if (input == null)
            {
                yield break;
            }

            var set = new HashSet<string>();
            foreach (var item in input)
            {
                if (!set.Contains(item))
                {
                    set.Add(item);
                    yield return item;
                }
            }
        }
    }
}
