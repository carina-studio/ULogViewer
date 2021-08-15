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
		static Dictionary<string, PropertyInfo> stringPropertyMap = new Dictionary<string, PropertyInfo>();
		static volatile IList<string>? stringPropertyNames;


		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			this.BeginningTimestamp = builder.GetDateTimeOrNull(nameof(BeginningTimestamp));
			this.Category = builder.GetStringOrNull(nameof(Category));
			this.DeviceId = builder.GetStringOrNull(nameof(DeviceId));
			this.DeviceName = builder.GetStringOrNull(nameof(DeviceName));
			this.EndingTimestamp = builder.GetDateTimeOrNull(nameof(EndingTimestamp));
			this.Event = builder.GetStringOrNull(nameof(Event));
			this.Extra1 = builder.GetStringOrNull(nameof(Extra1));
			this.Extra10 = builder.GetStringOrNull(nameof(Extra10));
			this.Extra2 = builder.GetStringOrNull(nameof(Extra2));
			this.Extra3 = builder.GetStringOrNull(nameof(Extra3));
			this.Extra4 = builder.GetStringOrNull(nameof(Extra4));
			this.Extra5 = builder.GetStringOrNull(nameof(Extra5));
			this.Extra6 = builder.GetStringOrNull(nameof(Extra6));
			this.Extra7 = builder.GetStringOrNull(nameof(Extra7));
			this.Extra8 = builder.GetStringOrNull(nameof(Extra8));
			this.Extra9 = builder.GetStringOrNull(nameof(Extra9));
			this.FileName = builder.GetStringOrNull(nameof(FileName));
			this.Id = Interlocked.Increment(ref nextId);
			this.Level = builder.GetEnumOrNull<LogLevel>(nameof(Level)) ?? LogLevel.Undefined;
			this.LineNumber = builder.GetInt32OrNull(nameof(LineNumber));
			this.Message = builder.GetStringOrNull(nameof(Message));
			this.ProcessId = builder.GetInt32OrNull(nameof(ProcessId));
			this.ProcessName = builder.GetStringOrNull(nameof(ProcessName));
			this.SourceName = builder.GetStringOrNull(nameof(SourceName));
			this.Summary = builder.GetStringOrNull(nameof(Summary));
			this.Tags = builder.GetStringOrNull(nameof(Tags));
			this.ThreadId = builder.GetInt32OrNull(nameof(ThreadId));
			this.ThreadName = builder.GetStringOrNull(nameof(ThreadName));
			this.Timestamp = builder.GetDateTimeOrNull(nameof(Timestamp));
			this.Title = builder.GetStringOrNull(nameof(Title));
			this.UserId = builder.GetStringOrNull(nameof(UserId));
			this.UserName = builder.GetStringOrNull(nameof(UserName));
		}


		/// <summary>
		/// Get beginning timestamp.
		/// </summary>
		public DateTime? BeginningTimestamp { get; }


		/// <summary>
		/// Get category of log.
		/// </summary>
		public string? Category { get; }


#pragma warning disable CS8603
#pragma warning disable CS8600
		/// <summary>
		/// Create <see cref="Func{T, TResult}"/> to get value of specific property.
		/// </summary>
		/// <typeparam name="T">Type of property value.</typeparam>
		/// <param name="propertyName">Property name.</param>
		/// <returns><see cref="Func{T, TResult}"/>.</returns>
		public static Func<Log, T> CreatePropertyGetter<T>(string propertyName)
		{
			SetupPropertyMap();
			if (propertyMap.TryGetValue(propertyName, out var propertyInfo))
				return (it => (T)propertyInfo.GetValue(it));
			return (it => default);
		}
#pragma warning restore CS8603
#pragma warning restore CS8600


		/// <summary>
		/// Get ID of device which generates log.
		/// </summary>
		public string? DeviceId { get; }


		/// <summary>
		/// Get name of device which generates log.
		/// </summary>
		public string? DeviceName { get; }


		/// <summary>
		/// Get ending timestamp.
		/// </summary>
		public DateTime? EndingTimestamp { get; }


		/// <summary>
		/// Get event of log.
		/// </summary>
		public string? Event { get; }


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public string? Extra1 { get; }


		/// <summary>
		/// Get 10th extra data of log.
		/// </summary>
		public string? Extra10 { get; }


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public string? Extra2 { get; }


		/// <summary>
		/// Get 3rd extra data of log.
		/// </summary>
		public string? Extra3 { get; }


		/// <summary>
		/// Get 4th extra data of log.
		/// </summary>
		public string? Extra4 { get; }


		/// <summary>
		/// Get 5th extra data of log.
		/// </summary>
		public string? Extra5 { get; }


		/// <summary>
		/// Get 6th extra data of log.
		/// </summary>
		public string? Extra6 { get; }


		/// <summary>
		/// Get 7th extra data of log.
		/// </summary>
		public string? Extra7 { get; }


		/// <summary>
		/// Get 8th extra data of log.
		/// </summary>
		public string? Extra8 { get; }


		/// <summary>
		/// Get 9th extra data of log.
		/// </summary>
		public string? Extra9 { get; }


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
		/// Check whether given log property is exported by <see cref="Log"/> with <see cref="string"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasStringProperty(string propertyName)
		{
			SetupPropertyMap();
			return stringPropertyMap.ContainsKey(propertyName);
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
								case nameof(Id):
								case nameof(PropertyNames):
								case nameof(StringPropertyNames):
									break;
								default:
									propertyMap[propertyInfo.Name] = propertyInfo;
									if (propertyInfo.PropertyType == typeof(string))
										stringPropertyMap[propertyInfo.Name] = propertyInfo;
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
		/// Get list of log properties exported by <see cref="Log"/> with <see cref="string"/> value.
		/// </summary>
		public static IList<string> StringPropertyNames
		{
			get
			{
				SetupPropertyMap();
				if (stringPropertyNames == null)
				{
					lock (typeof(Log))
					{
						if (stringPropertyNames == null)
							stringPropertyNames = stringPropertyMap.Keys.ToArray().AsReadOnly();
					}
				}
				return stringPropertyNames;
			}
		}


		/// <summary>
		/// Get summary of log.
		/// </summary>
		public string? Summary { get; }


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public string? Tags { get; }


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
		/// Get title of log.
		/// </summary>
		public string? Title { get; }


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
