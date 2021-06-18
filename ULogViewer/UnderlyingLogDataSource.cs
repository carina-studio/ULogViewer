using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Low-level source of log data.
	/// </summary>
	enum UnderlyingLogDataSource
	{
		/// <summary>
		/// Undefined.
		/// </summary>
		Undefined,
		/// <summary>
		/// File.
		/// </summary>
		File,
		/// <summary>
		/// Network.
		/// </summary>
		Network,
		/// <summary>
		/// Standard output of process.
		/// </summary>
		StandardOutput,
	}
}
