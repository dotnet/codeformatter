using EditorConfig.Core;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.CodeFormatting
{
    interface IEditorConfigProvider
    {
        FileConfiguration GetConfiguration(Document document);
        FileConfiguration GetConfiguration(TextDocument document);
    }
}
