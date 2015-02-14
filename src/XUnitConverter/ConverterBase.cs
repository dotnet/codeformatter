using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitConverter
{
    public abstract class ConverterBase
    {
        public async Task<Solution> ProcessAsync(Project project, CancellationToken cancellationToken)
        {
            var solution = project.Solution;
            foreach (var id in project.DocumentIds)
            {
                var document = solution.GetDocument(id);
                var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken);
                if (syntaxNode == null)
                {
                    continue;
                }

                solution = await ProcessAsync(document, syntaxNode, cancellationToken);
            }

            return solution;
        }

        protected abstract Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken);
    }
}
