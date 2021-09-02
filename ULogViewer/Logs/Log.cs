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
		/// <summary>
		/// Get capacity of extra data.
		/// </summary>
		public const int ExtraCapacity = 10;


		// Static fields.
		static Dictionary<string, PropertyInfo> dateTimePropertyMap = new Dictionary<string, PropertyInfo>();
		static readonly CompressedString?[] emptyCompressedStringArray = new CompressedString[0];
		static volatile bool isPropertyMapReady;
		static long nextId = 0;
		static Dictionary<string, PropertyInfo> propertyMap = new Dictionary<string, PropertyInfo>();
		static volatile IList<string>? propertyNames;
		static Dictionary<string, PropertyInfo> stringPropertyMap = new Dictionary<string, PropertyInfo>();
		static volatile IList<string>? stringPropertyNames;


		// Fields.
		readonly CompressedString? category;
		readonly CompressedString? deviceId;
		readonly CompressedString? deviceName;
		readonly CompressedString? eventString;
		readonly CompressedString?[] extras;
		readonly CompressedString? message;
		readonly CompressedString? processName;
		readonly CompressedString? sourceName;
		readonly CompressedString? summary;
		readonly CompressedString? tags;
		readonly CompressedString? threadName;
		readonly CompressedString? title;
		readonly CompressedString? userId;
		readonly CompressedString? userName;


		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			this.BeginningTimestamp = builder.GetDateTimeOrNull(nameof(BeginningTimestamp));
			this.category = builder.GetCompressedStringOrNull(nameof(Category));
			this.deviceId = builder.GetCompressedStringOrNull(nameof(DeviceId));
			this.deviceName = builder.GetCompressedStringOrNull(nameof(DeviceName));
			this.EndingTimestamp = builder.GetDateTimeOrNull(nameof(EndingTimestamp));
			this.eventString = builder.GetCompressedStringOrNull(nameof(Event));
			var extraCount = builder.MaxExtraNumber;
			if (extraCount > 0)
			{
				this.extras = new CompressedString?[extraCount];
				for (var i = extraCount; i > 0; --i)
					this.extras[i - 1] = builder.GetCompressedStringOrNull($"Extra{i}");
			}
			else
				this.extras = emptyCompressedStringArray;
			this.FileName = builder.GetStringOrNull(nameof(FileName));
			this.Id = Interlocked.Increment(ref nextId);
			this.Level = builder.GetEnumOrNull<LogLevel>(nameof(Level)) ?? LogLevel.Undefined;
			this.LineNumber = builder.GetInt32OrNull(nameof(LineNumber));
			this.message = builder.GetCompressedStringOrNull(nameof(Message));
			this.ProcessId = builder.GetInt32OrNull(nameof(ProcessId));
			this.processName = builder.GetCompressedStringOrNull(nameof(ProcessName));
			this.sourceName = builder.GetCompressedStringOrNull(nameof(SourceName));
			this.summary = builder.GetCompressedStringOrNull(nameof(Summary));
			this.tags = builder.GetCompressedStringOrNull(nameof(Tags));
			this.ThreadId = builder.GetInt32OrNull(nameof(ThreadId));
			this.threadName = builder.GetCompressedStringOrNull(nameof(ThreadName));
			this.Timestamp = builder.GetDateTimeOrNull(nameof(Timestamp));
			this.title = builder.GetCompressedStringOrNull(nameof(Title));
			this.userId = builder.GetCompressedStringOrNull(nameof(UserId));
			this.userName = builder.GetCompressedStringOrNull(nameof(UserName));
		}


		/// <summary>
		/// Get beginning timestamp.
		/// </summary>
		public DateTime? BeginningTimestamp { get; }


		/// <summary>
		/// Get category of log.
		/// </summary>
		public string? Category { get => this.category?.ToString(); }


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
		public string? DeviceId { get => this.deviceId?.ToString(); }


		/// <summary>
		/// Get name of device which generates log.
		/// </summary>
		public string? DeviceName { get => this.deviceName?.ToString(); }


		/// <summary>
		/// Get ending timestamp.
		/// </summary>
		public DateTime? EndingTimestamp { get; }


		/// <summary>
		/// Get event of log.
		/// </summary>
		public string? Event { get => this.eventString?.ToString(); }


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public string? Extra1 { get => this.extras.Length > 0 ? this.extras[0]?.ToString() : null; }


		/// <summary>
		/// Get 10th extra data of log.
		/// </summary>
		public string? Extra10 { get => this.extras.Length > 9 ? this.extras[9]?.ToString() : null; }


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public string? Extra2 { get => this.extras.Length > 1 ? this.extras[1]?.ToString() : null; }


		/// <summary>
		/// Get 3rd extra data of log.
		/// </summary>
		public string? Extra3 { get => this.extras.Length > 2 ? this.extras[2]?.ToString() : null; }


		/// <summary>
		/// Get 4th extra data of log.
		/// </summary>
		public string? Extra4 { get => this.extras.Length > 3 ? this.extras[3]?.ToString() : null; }


		/// <summary>
		/// Get 5th extra data of log.
		/// </summary>
		public string? Extra5 { get => this.extras.Length > 4 ? this.extras[4]?.ToString() : null; }


		/// <summary>
		/// Get 6th extra data of log.
		/// </summary>
		public string? Extra6 { get => this.extras.Length > 5 ? this.extras[5]?.ToString() : null; }


		/// <summary>
		/// Get 7th extra data of log.
		/// </summary>
		public string? Extra7 { get => this.extras.Length > 6 ? this.extras[6]?.ToString() : null; }


		/// <summary>
		/// Get 8th extra data of log.
		/// </summary>
		public string? Extra8 { get => this.extras.Length > 7 ? this.extras[7]?.ToString() : null; }


		/// <summary>
		/// Get 9th extra data of log.
		/// </summary>
		public string? Extra9 { get => this.extras.Length > 8 ? this.extras[8]?.ToString() : null; }


		/// <summary>
		/// Get name of file which log read from.
		/// </summary>
		public string? FileName { get; }


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> with <see cref="DateTime"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasDateTimeProperty(string propertyName)
		{
			SetupPropertyMap();
			return dateTimePropertyMap.ContainsKey(propertyName);
		}


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> with multi-line <see cref="string"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasMultiLineStringProperty(string propertyName) => propertyName switch
		{
			nameof(Message)
			or nameof(Summary)
			or nameof(Extra1)
			or nameof(Extra2)
			or nameof(Extra3)
			or nameof(Extra4)
			or nameof(Extra5)
			or nameof(Extra6)
			or nameof(Extra7)
			or nameof(Extra8)
			or nameof(Extra9)
			or nameof(Extra10) => true,
			_ => false,
		};


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
		public string? Message { get => this.message?.ToString(); }


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId { get; }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public string? ProcessName { get => this.processName?.ToString(); }


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
									else if (propertyInfo.PropertyType == typeof(DateTime?) || propertyInfo.PropertyType == typeof(DateTime))
										dateTimePropertyMap[propertyInfo.Name] = propertyInfo;
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
		public string? SourceName { get => this.sourceName?.ToString(); }


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
		public string? Summary { get => this.summary?.ToString(); }


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public string? Tags { get => this.tags?.ToString(); }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId { get; }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public string? ThreadName { get => this.threadName?.ToString(); }


		/// <summary>
		/// Get timestamp.
		/// </summary>
		public DateTime? Timestamp { get; }


		/// <summary>
		/// Get title of log.
		/// </summary>
		public string? Title { get => this.title?.ToString(); }


#pragma warning disable CS8600
#pragma warning disable CS8601
		/// <summary>
		/// Get get property of log by name.
		/// </summary>
		/// <typeparam name="T">Type of property.</typeparam>
		/// <param name="propertyName">Name of property.</param>
		/// <param name="value">Property value.</param>
		/// <returns>True if value of property get successfully.</returns>
		public bool TryGetProperty<T>(string propertyName, out T value)
		{
			SetupPropertyMap();
			if (propertyMap.TryGetValue(propertyName, out var propertyInfo) 
				&& propertyInfo != null 
				&& typeof(T).IsAssignableFrom(propertyInfo.PropertyType))
			{
				value = (T)propertyInfo.GetValue(this);
				return true;
			}
			value = default;
			return false;
		}
#pragma warning restore CS8600
#pragma warning restore CS8601


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public string? UserId { get => this.userId?.ToString(); }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public string? UserName { get => this.userName?.ToString(); }
	}
}
