using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadCodeAnalysis
{
    public class DocumentConditionalRegionInfo : IComparable<DocumentConditionalRegionInfo>, IEquatable<DocumentConditionalRegionInfo>
    {
        public Document Document { get; private set; }

        public List<ConditionalRegion> Regions { get; private set; }

        public DocumentConditionalRegionInfo(Document document, List<ConditionalRegion> regions)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (regions == null)
            {
                throw new ArgumentNullException("chains");
            }

            Document = document;
            Regions = regions;

            // Sort the list of regions to make sure that nested chains of directives are not out of order with respect
            // to their parents.
            Regions.Sort();
        }

        public void Intersect(DocumentConditionalRegionInfo other)
        {
            if (!Equals(other))
            {
                return;
            }

            Debug.Assert(Regions.Count == other.Regions.Count);
            for (int i = 0; i < Regions.Count; i++)
            {
                Regions[i].Intersect(other.Regions[i]);
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
