using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Represents a single log.
	/// </summary>
	interface ILog
	{
		/// <summary>
		/// Get level.
		/// </summary>
		LogLevel Level { get; }


		/// <summary>
		/// Get message.
		/// </summary>
		string? Message { get; }


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		int? ProcessId { get; }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		string? ProcessName { get; }


		/// <summary>
		/// Get name of source which generates log.
		/// </summary>
		string? SourceName { get; }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		int? ThreadId { get; }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		string? ThreadName { get; }


		/// <summary>
		/// Get timestamp.
		/// </summary>
		DateTime? Timestamp { get; }


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		string? UserId { get; }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		string? UserName { get; }
	}
}
