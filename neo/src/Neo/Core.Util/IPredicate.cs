using System;

namespace Neo.Core.Util
{
	public interface IPredicate
	{
		bool IsTrueForValue(object aValue, object nullVal);
		object Value { get; } // this shouldn't really be on the interface a not predicate wouldn't have a value
	}

}
