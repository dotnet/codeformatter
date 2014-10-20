using System;
using System.ComponentModel.Composition;

namespace CodeFormatter.Engine
{
    [AttributeUsage(AttributeTargets.Class)]
    [MetadataAttribute]
    public sealed class ExportFormattingRuleAttribute : ExportAttribute, IOrderMetadata
    {
        private readonly int _order;

        public ExportFormattingRuleAttribute(int order)
            : base(typeof(IFormattingRule))
        {
            _order = order;
        }

        public int Order
        {
            get { return _order; }
        }
    }
}