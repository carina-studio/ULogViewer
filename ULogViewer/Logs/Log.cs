using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Represents a single log.
	/// </summary>
	class Log
	{
		// Static fields.
		static volatile bool isPropertyMapReady;
		static long nextId = 0;
		static Dictionary<string, PropertyInfo> propertyMap = new Dictionary<string, PropertyInfo>();
		static volatile IList<string>? propertyNames;


		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			this.FileName = builder.GetStringOrNull(nameof(FileName));
			this.Id = Interlocked.Increment(ref nextId);
			this.Level = builder.GetEnumOrNull<LogLevel>(nameof(Level)) ?? LogLevel.Undefined;
			this.LineNumber = builder.GetInt32OrNull(nameof(LineNumber));
			this.Message = builder.GetStringOrNull(nameof(Message));
			this.ProcessId = builder.GetInt32OrNull(nameof(ProcessId));
			this.ProcessName = builder.GetStringOrNull(nameof(ProcessName));
			this.Reader = builder.Reader;
			this.SourceName = builder.GetStringOrNull(nameof(SourceName));
			this.ThreadId = builder.GetInt32OrNull(nameof(ThreadId));
			this.ThreadName = builder.GetStringOrNull(nameof(ThreadName));
			this.Timestamp = builder.GetDateTimeOrNull(nameof(Timestamp));
			this.UserId = builder.GetStringOrNull(nameof(UserId));
			this.UserName = builder.GetStringOrNull(nameof(UserName));
		}


		/// <summary>
		/// Get name of file which log read from.
		/// </summary>
		public string? FileName { get; }


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasProperty(string propertyName)
		{
			SetupPropertyMap();
			return propertyMap.ContainsKey(propertyName);
		}


		/// <summary>
		/// Get unique incremental ID of this instance.
		/// </summary>
		public long Id { get; }


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
		/// Get list of log properties exported by <see cref="Log"/>.
		/// </summary>
		public static IList<string> PropertyNames
		{
			get
			{
				SetupPropertyMap();
				if (propertyNames == null)
				{
					lock (typeof(Log))
					{
						if (propertyNames == null)
							propertyNames = propertyMap.Keys.ToArray().AsReadOnly();
					}
				}
				return propertyNames;
			}
		}


		/// <summary>
		/// Get <see cref="LogReader"/> which generates this instance.
		/// </summary>
		public LogReader? Reader { get; }


		// Setup property map.
		static void SetupPropertyMap()
		{
			if (!isPropertyMapReady)
			{
				lock (typeof(Log))
				{
					if (!isPropertyMapReady)
					{
						foreach (var propertyInfo in typeof(Log).GetProperties())
						{
							switch(propertyInfo.Name)
							{
								case "Id":
								case "Reader":
									break;
								default:
									propertyMap[propertyInfo.Name] = propertyInfo;
									break;
							}
						}
						isPropertyMapReady = true;
					}
				}
			}
		}


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
