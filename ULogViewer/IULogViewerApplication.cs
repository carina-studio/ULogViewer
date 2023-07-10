using CarinaStudio.AppSuite;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Interface of ULogViewer application.
	/// </summary>
	interface IULogViewerApplication : IAppSuiteApplication
	{
		/// <summary>
		/// Get instance of <see cref="IULogViewerApplication"/> of current process.
		/// </summary>
		public static new IULogViewerApplication Current => (IULogViewerApplication)IAppSuiteApplication.Current;
	}
}
