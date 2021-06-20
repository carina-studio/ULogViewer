using System;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Represents a single log.
	/// </summary>
	class Log
	{
		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			this.Level = builder.GetEnumOrNull<LogLevel>(nameof(Level)) ?? LogLevel.Undefined;
			this.LineNumber = builder.GetInt32OrNull(nameof(LineNumber));
			this.Message = builder.GetStringOrNull(nameof(Message));
			this.ProcessId = builder.GetInt32OrNull(nameof(ProcessId));
			this.ProcessName = builder.GetStringOrNull(nameof(ProcessName));
			this.SourceName = builder.GetStringOrNull(nameof(SourceName));
			this.ThreadId = builder.GetInt32OrNull(nameof(ThreadId));
			this.ThreadName = builder.GetStringOrNull(nameof(ThreadName));
			this.Timestamp = builder.GetDateTimeOrNull(nameof(Timestamp));
			this.UserId = builder.GetStringOrNull(nameof(UserId));
			this.UserName = builder.GetStringOrNull(nameof(UserName));
		}


		/// <summary>
		/// Get level.
		/// </summary>
		public LogLevel Level { get; }


		/// <summary>
		/// Get line number.
		/// </summary>
		public int? LineNumber { get; }


		/// <summary>
		/// Get message.
		/// </summary>
		public string? Message { get; }


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId { get; }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public string? ProcessName { get; }


		/// <summary>
		/// Get name of source which generates log.
		/// </summary>
		public string? SourceName { get; }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId { get; }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public string? ThreadName { get; }


		/// <summary>
		/// Get timestamp.
		/// </summary>
		public DateTime? Timestamp { get; }


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public string? UserId { get; }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public string? UserName { get; }
	}
}
