using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    /// <summary>
    /// Represents a list of conditional region chains in a given document in prefix document order.
    /// </summary>
    public class DocumentConditionalRegionInfo : IComparable<DocumentConditionalRegionInfo>, IEquatable<DocumentConditionalRegionInfo>
    {
        public Document Document { get; private set; }

        // TODO: It might be cleaner if there were a struct for a chain
        public List<List<ConditionalRegion>> Chains { get; private set; }

        internal DocumentConditionalRegionInfo(Document document, List<List<ConditionalRegion>> chains)
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

        internal void Intersect(DocumentConditionalRegionInfo other)
        {
            if (!Equals(other))
            {
                return;
            }

            Debug.Assert(Chains.Count == other.Chains.Count);
            for (int i = 0; i < Chains.Count; i++)
            {
                var chainA = Chains[i];
                var chainB = other.Chains[i];
                Debug.Assert(chainA.Count == chainB.Count);

                bool conditionVaries = false;

                for (int j = 0; j < chainA.Count; i++)
                {
                    var region = chainA[j];
                    region.Intersect(chainB[j]);

                    // If the condition of a region varies, then the conditions of all following regions in the chain
                    // are implicitly varying.
                    if (conditionVaries || region.State == ConditionalRegionState.Varying)
                    {
                        conditionVaries = true;
                        region.State = ConditionalRegionState.Varying;
                    }
                }
            }
        }

        public int CompareTo(DocumentConditionalRegionInfo other)
        {
            if (other == null)
            {
                return 1;
            }

            return string.Compare(Document.FilePath, other.Document.FilePath, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool Equals(DocumentConditionalRegionInfo other)
        {
            return CompareTo(other) == 0;
        }
    }
}
