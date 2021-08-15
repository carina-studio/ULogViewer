using Avalonia.Controls;
using System;
using System.Collections;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Extensions for <see cref="ItemsControl"/>.
	/// </summary>
	static class ItemsControlExtensions
	{
		/// <summary>
		/// Get number of item in <see cref="ItemsControl"/>.
		/// </summary>
		/// <param name="itemsControl"><see cref="ItemsControl"/>.</param>
		/// <returns>Number of items, or 0 if number of items cannot be determined.</returns>
		public static int GetItemCount(this ItemsControl itemsControl) => itemsControl.Items?.Let(it =>
		{
			if (it is ICollection collection)
				return collection.Count;
			try
			{
				return it.GetType().GetProperty("Count")?.Let(property =>
				{
					if (property.PropertyType == typeof(int))
						return (int)property.GetValue(it).AsNonNull();
					return 0;
				}) ?? 0;
			}
			catch
			{ }
			return 0;
		}) ?? 0;
	}
}
