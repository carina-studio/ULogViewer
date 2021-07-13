using Avalonia.Input;
using System;

namespace CarinaStudio.ULogViewer.Input
{
	/// <summary>
	/// Extensions for <see cref="IDataObject"/>.
	/// </summary>
	static class DataObjectExtensions
	{
		/// <summary>
		/// Check whether at least one file name is contained in <see cref="IDataObject"/> or not.
		/// </summary>
		/// <param name="data"><see cref="IDataObject"/>.</param>
		/// <returns>True if at least one file name is contained in <see cref="IDataObject"/>.</returns>
		public static bool HasFileNames(this IDataObject data) => data.GetFileNames()?.Let(it =>
		{
			foreach (var _ in it)
				return true;
			return false;
		}) ?? false;
	}
}
