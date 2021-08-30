using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application startup parameters.
	/// </summary>
	struct AppStartupParams
	{
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
