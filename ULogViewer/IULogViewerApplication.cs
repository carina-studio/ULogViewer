using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Interface of ULogViewer application.
	/// </summary>
	interface IULogViewerApplication : AppSuite.IAppSuiteApplication
	{
		/// <summary>
		/// Check whether application is running for testing purpose or not.
		/// </summary>
		bool IsTesting { get; }
	}
}
