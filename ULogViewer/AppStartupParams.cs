using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application startup parameters.
	/// </summary>
	struct AppStartupParams
	{
		/// <summary>
		/// ID of initial log profile.
		/// </summary>
		public string? LogProfileId { get; set; }
	}
}
