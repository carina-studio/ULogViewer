using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application startup parameters.
	/// </summary>
	struct AppStartupParams
	{
		/// <summary>
		/// Whether instance state should be restored or not/
		/// </summary>
		public bool IsRestoringStateRequested { get; set; }


		/// <summary>
		/// Launch in debug mode.
		/// </summary>
		public bool LaunchInDebugMode { get; set; }


		/// <summary>
		/// ID of initial log profile.
		/// </summary>
		public string? LogProfileId { get; set; }
	}
}
