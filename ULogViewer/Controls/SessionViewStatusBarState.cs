using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// State of status bar in <see cref="SessionView"/>.
	/// </summary>
	enum SessionViewStatusBarState
	{
		/// <summary>
		/// None.
		/// </summary>
		None,
		/// <summary>
		/// Active.
		/// </summary>
		Active,
		/// <summary>
		/// Warning.
		/// </summary>
		Warning,
		/// <summary>
		/// Error occurred.
		/// </summary>
		Error,
		/// <summary>
		/// Paused.
		/// </summary>
		Paused,
	}
}
