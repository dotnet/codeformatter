using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public partial class AnalysisEngine
    {
        public class Options
        {
            public IEnumerable<Project> Projects { get; private set; }

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

            internal Options(
                IEnumerable<Project> projects = null,
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
                Projects = projects;
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
}
