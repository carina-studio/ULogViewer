using System;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Encoding of timestamp of log.
	/// </summary>
	enum LogTimestampEncoding
	{
		/// <summary>
		/// Custom format.
		/// </summary>
		Custom,
		/// <summary>
		/// Unix timestamp.
		/// </summary>
		Unix,
		/// <summary>
		/// Unix timestamp in milliseconds.
		/// </summary>
		UnixMilliseconds,
		/// <summary>
		/// Unix timestamp in microseconds.
		/// </summary>
		UnixMicroseconds,
	}
}
