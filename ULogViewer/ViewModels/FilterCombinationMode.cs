using System;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Mode to combine multiple filters.
	/// </summary>
	enum FilterCombinationMode
	{
		/// <summary>
		/// Combine filtered items automatically.
		/// </summary>
		Auto,
		/// <summary>
		/// Get intersection of filtered items.
		/// </summary>
		Intersection,
		/// <summary>
		/// Get union of filtered items.
		/// </summary>
		Union,
	}
}
