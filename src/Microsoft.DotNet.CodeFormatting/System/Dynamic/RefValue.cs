using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Dynamic
{
	/// <summary>
	/// Allows by-ref values to be passed to reflection dynamic.
	/// This support does not exist in C# 4.0 dynamic out of the box.
	/// </summary>
	abstract partial class RefValue
	{
		/// <summary>
		/// Creates a value getter/setter delegating reference
		/// to be used by reference when invoking the 
		/// dynamic object.
		/// </summary>
		/// <param name="getter">The getter of the by-ref value to the dynamic invocation.</param>
		/// <param name="setter">The setter of the by-ref value to the dynamic invocation.</param>
		public static RefValue<T> Create<T>(Func<T> getter, Action<T> setter)
		{
			return new RefValue<T>(getter, setter);
		}

		/// <summary>
		/// Gets or sets the value.
		/// </summary>
		internal abstract object Value { get; set; }
	}

	/// <summary>
	/// Allows by-ref values to be passed to reflection dynamic.
	/// This support does not exist in C# 4.0 dynamic out of the box.
	/// </summary>
	partial class RefValue<T> : RefValue
	{
		private Func<T> getter;
		private Action<T> setter;

		/// <summary>
		/// Initializes a new instance of the <see cref="RefValue&lt;T&gt;"/> class.
		/// </summary>
		public RefValue(Func<T> getter, Action<T> setter)
		{
			this.getter = getter;
			this.setter = setter;
		}

		/// <summary>
		/// Gets or sets the value.
		/// </summary>
		internal override object Value
		{
			get { return this.getter(); }
			set { this.setter((T)value); }
		}
	}
}
