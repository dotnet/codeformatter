using System.ComponentModel;

namespace Microsoft.DotNet.CodeFormatting
{
    public interface IOrderMetadata
    {
        [DefaultValue(int.MaxValue)]
        int Order { get; }
    }
}
