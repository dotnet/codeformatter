using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    /// <summary>
    /// Represents a list of conditional region chains in a given document in prefix document order.
    /// </summary>
    public class DocumentConditionalRegionInfo : IComparable<DocumentConditionalRegionInfo>, IEquatable<DocumentConditionalRegionInfo>
    {
        public Document Document { get; private set; }

        public List<ConditionalRegionChain> Chains { get; private set; }

        internal DocumentConditionalRegionInfo(Document document, List<ConditionalRegionChain> chains)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (chains == null)
            {
                throw new ArgumentNullException("chains");
            }

            Document = document;
            Chains = chains;
        }

        public int CompareTo(DocumentConditionalRegionInfo other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = string.Compare(Document.FilePath, other.Document.FilePath, StringComparison.InvariantCultureIgnoreCase);
            if (result == 0)
            {
                return Chains.Count - other.Chains.Count;
            }

            return result;
        }

        public bool Equals(DocumentConditionalRegionInfo other)
        {
            return CompareTo(other) == 0;
        }
    }
}
