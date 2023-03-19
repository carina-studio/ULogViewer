using System;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Level of log.
	/// </summary>
	public enum LogLevel
	{
		/// <summary>
		/// Undefined.
		/// </summary>
		Undefined,
		/// <summary>
		/// Verbose.
		/// </summary>
		Verbose,
		/// <summary>
		/// Tracing.
		/// </summary>
		Trace,
		/// <summary>
		/// Debug.
		/// </summary>
		Debug,
		/// <summary>
		/// Info.
		/// </summary>
		Info,
		/// <summary>
		/// Success.
		/// </summary>
		Success,
		/// <summary>
		/// Warn.
		/// </summary>
		Warn,
		/// <summary>
		/// Failure.
		/// </summary>
		Failure,
		/// <summary>
		/// Error.
		/// </summary>
		Error,
		/// <summary>
		/// Fatal.
		/// </summary>
		Fatal,
	}
}
