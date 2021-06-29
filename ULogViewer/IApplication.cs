using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Interface of ULogViewer application.
	/// </summary>
	interface IApplication : CarinaStudio.IApplication
	{
		/// <summary>
		/// Check whether application is running for testing purpose or not.
		/// </summary>
		bool IsTesting { get; }


		/// <summary>
		/// Raised after string resources are updated.
		/// </summary>
		event EventHandler StringsUpdated;
	}
}
