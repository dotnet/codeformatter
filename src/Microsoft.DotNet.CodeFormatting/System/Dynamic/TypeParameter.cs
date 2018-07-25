using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Dynamic
{
	/// <summary>
	/// Represents a runtime type parameter to use to 
	/// make a generic method invocation.
	/// </summary>
	partial class TypeParameter
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TypeParameter"/> class.
		/// </summary>
		public TypeParameter(Type type)
		{
			this.Type = type;
		}

		/// <summary>
		/// Gets the type.
		/// </summary>
		public Type Type { get; private set; }
	}
}
