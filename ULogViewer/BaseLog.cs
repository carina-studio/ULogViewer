using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Base implementation of <see cref="ILog"/>.
	/// </summary>
	abstract class BaseLog : ILog
	{
		/// <summary>
		/// Initialize new <see cref="BaseLog"/> instance.
		/// </summary>
		protected BaseLog()
		{ }


		/// <summary>
		/// Get level.
		/// </summary>
		public abstract LogLevel Level { get; }


		/// <summary>
		/// Get message.
		/// </summary>
		public virtual string? Message { get; }


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public virtual int? ProcessId { get; }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public virtual string? ProcessName { get; }


		/// <summary>
		/// Get name of source which generates log.
		/// </summary>
		public virtual string? SourceName { get; }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public virtual int? ThreadId { get; }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public virtual string? ThreadName { get; }


		/// <summary>
		/// Get timestamp.
		/// </summary>
		public virtual DateTime? Timestamp { get; }


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public virtual string? UserId { get; }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public virtual string? UserName { get; }
	}
}
