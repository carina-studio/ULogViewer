using CarinaStudio.Collections;
using CarinaStudio.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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


		// Property definitions.
		enum PropertyName
		{
			BeginningTimeSpan,
			BeginningTimestamp,
			Category,
			DeviceId,
			DeviceName,
			EndingTimeSpan,
			EndingTimestamp,
			Event,
			Extra1,
			Extra10,
			Extra2,
			Extra3,
			Extra4,
			Extra5,
			Extra6,
			Extra7,
			Extra8,
			Extra9,
			FileName,
			Level,
			LineNumber,
			Message,
			ProcessId,
			ProcessName,
			ReadTime,
			SourceName,
			Summary,
			Tags,
			ThreadId,
			ThreadName,
			TimeSpan,
			Timestamp,
			Title,
			UserId,
			UserName,
		}


		// Static fields.
		static readonly long baseMemorySize = Memory.EstimateInstanceSize<Log>();
		static readonly HashSet<string> dateTimePropertyNameSet = new();
		static volatile bool isPropertyMapReady;
		static long nextId = 0;
		static readonly Dictionary<string, int> propertyIndices = new();
		static readonly Dictionary<string, PropertyInfo> propertyMap = new();
		static readonly int propertyNameCount;
		static readonly IList<string> propertyNames = Enum.GetValues<PropertyName>().Let(propertyNames =>
		{
			var propertyCount = propertyNames.Length;
			return new List<string>(propertyCount).Also(it =>
			{
				for (var i = 0; i < propertyCount; ++i)
					it.Add(propertyNames[i].ToString());
			}).AsReadOnly();
		});
		[ThreadStatic]
		static Dictionary<string, LogStringPropertyGetter>? stringPropertyGetters;
		static readonly HashSet<string> stringPropertyNameSet = new();
		static readonly HashSet<string> timeSpanPropertyNameSet = new();


		// Fields.
		readonly byte[] propertyValueIndices;
		readonly object?[] propertyValues;


		// Static initializer.
		static Log()
		{
			for (var i = propertyNames.Count - 1; i >= 0; --i)
				propertyIndices[propertyNames[i]] = i;
			propertyNameCount = propertyNames.Count;
		}


		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			// prepare
			var propertyValueIndices = new byte[propertyNameCount];
			var propertyValues = new object?[builder.PropertyCount + 1]; // Including ReadTime
			var index = 0;
			int propertyIndex;
			long propertyMemorySize = 0;
			foreach (var propertyName in builder.PropertyNames)
			{
				var value = GetPropertyFromBuilder(builder, propertyName);
				if (value == null)
					continue;
				if (!propertyIndices.TryGetValue(propertyName, out propertyIndex))
					continue;
				propertyValueIndices[propertyIndex] = (byte)(index + 1);
				propertyValues[index++] = value;
				if (value is CompressedString compressedString)
					propertyMemorySize += compressedString.Size;
				else if (value is string str)
					propertyMemorySize += Memory.EstimateInstanceSize(typeof(string), str.Length);
				else
					propertyMemorySize += Memory.EstimateInstanceSize(value);
			}
			this.propertyValueIndices = propertyValueIndices;
			this.propertyValues = propertyValues;

			// get ID
			this.Id = Interlocked.Increment(ref nextId);

			// setup reading time
			if (propertyIndices.TryGetValue(nameof(ReadTime), out propertyIndex))
			{
				propertyValueIndices[propertyIndex] = (byte)(index + 1);
				propertyValues[index++] = DateTime.Now;
				propertyMemorySize += Memory.EstimateInstanceSize<DateTime>();
			}

			// calculate memory size
			this.MemorySize = baseMemorySize + propertyMemorySize + Memory.EstimateArrayInstanceSize(sizeof(byte), propertyValueIndices.Length) + Memory.EstimateArrayInstanceSize(IntPtr.Size, this.propertyValues.Length);
		}


		/// <summary>
		/// Get beginning time span.
		/// </summary>
		public TimeSpan? BeginningTimeSpan { get => (TimeSpan?)this.GetProperty(PropertyName.BeginningTimeSpan); }


		/// <summary>
		/// Get beginning timestamp.
		/// </summary>
		public DateTime? BeginningTimestamp { get => (DateTime?)this.GetProperty(PropertyName.BeginningTimestamp); }


		/// <summary>
		/// Get category of log.
		/// </summary>
		public string? Category { get => this.GetProperty(PropertyName.Category)?.ToString(); }


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
		/// Create delegate of getting specific string property of log.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>Getter of specific string property.</returns>
		public static LogStringPropertyGetter CreateStringPropertyGetter(string propertyName)
		{
			LogStringPropertyGetter? getter;
			if (stringPropertyGetters == null)
				stringPropertyGetters = new();
			else if (stringPropertyGetters.TryGetValue(propertyName, out getter))
				return getter;
			if (propertyIndices.TryGetValue(propertyName, out var propertyIndex))
			{
				getter = (log, buffer, offset) =>
				{
					if (log.GetProperty((PropertyName)propertyIndex) is CompressedString compressedString)
						return compressedString.GetString(buffer, offset);
					return 0;
				};
				stringPropertyGetters[propertyName] = getter;
				return getter;
			}
			return (log, buffer, offset) => 0;
		}


		/// <summary>
		/// Get ID of device which generates log.
		/// </summary>
		public string? DeviceId { get => this.GetProperty(PropertyName.DeviceId)?.ToString(); }


		/// <summary>
		/// Get name of device which generates log.
		/// </summary>
		public string? DeviceName { get => this.GetProperty(PropertyName.DeviceName)?.ToString(); }


		/// <summary>
		/// Get ending time span.
		/// </summary>
		public TimeSpan? EndingTimeSpan { get => (TimeSpan?)this.GetProperty(PropertyName.EndingTimeSpan); }


		/// <summary>
		/// Get ending timestamp.
		/// </summary>
		public DateTime? EndingTimestamp { get => (DateTime?)this.GetProperty(PropertyName.EndingTimestamp); }


		/// <summary>
		/// Get event of log.
		/// </summary>
		public string? Event { get => this.GetProperty(PropertyName.Event)?.ToString(); }


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public string? Extra1 { get => this.GetProperty(PropertyName.Extra1)?.ToString(); }


		/// <summary>
		/// Get 10th extra data of log.
		/// </summary>
		public string? Extra10 { get => this.GetProperty(PropertyName.Extra10)?.ToString(); }


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public string? Extra2 { get => this.GetProperty(PropertyName.Extra2)?.ToString(); }


		/// <summary>
		/// Get 3rd extra data of log.
		/// </summary>
		public string? Extra3 { get => this.GetProperty(PropertyName.Extra3)?.ToString(); }


		/// <summary>
		/// Get 4th extra data of log.
		/// </summary>
		public string? Extra4 { get => this.GetProperty(PropertyName.Extra4)?.ToString(); }


		/// <summary>
		/// Get 5th extra data of log.
		/// </summary>
		public string? Extra5 { get => this.GetProperty(PropertyName.Extra5)?.ToString(); }


		/// <summary>
		/// Get 6th extra data of log.
		/// </summary>
		public string? Extra6 { get => this.GetProperty(PropertyName.Extra6)?.ToString(); }


		/// <summary>
		/// Get 7th extra data of log.
		/// </summary>
		public string? Extra7 { get => this.GetProperty(PropertyName.Extra7)?.ToString(); }


		/// <summary>
		/// Get 8th extra data of log.
		/// </summary>
		public string? Extra8 { get => this.GetProperty(PropertyName.Extra8)?.ToString(); }


		/// <summary>
		/// Get 9th extra data of log.
		/// </summary>
		public string? Extra9 { get => this.GetProperty(PropertyName.Extra9)?.ToString(); }


		/// <summary>
		/// Get name of file which log read from.
		/// </summary>
		public string? FileName { get => this.GetProperty(PropertyName.FileName)?.ToString(); }


		// Get property.
		object? GetProperty(PropertyName propertyName)
		{
			int index = this.propertyValueIndices[(int)propertyName];
			if (index > 0)
				return this.propertyValues[index - 1];
			return null;
		}


		// Get property from log builder.
		static object? GetPropertyFromBuilder(LogBuilder builder, string propertyName) => propertyName switch
		{
			nameof(BeginningTimeSpan)
			or nameof(EndingTimeSpan)
			or nameof(TimeSpan) => builder.GetTimeSpanOrNull(propertyName),
			nameof(BeginningTimestamp)
			or nameof(EndingTimestamp)
			or nameof(Timestamp) => builder.GetDateTimeOrNull(propertyName),
			nameof(FileName) => builder.GetStringOrNull(propertyName),
			nameof(Level) => builder.GetEnumOrNull<LogLevel>(propertyName) ?? LogLevel.Undefined,
			nameof(LineNumber)
			or nameof(ProcessId)
			or nameof(ThreadId) => builder.GetInt32OrNull(propertyName),
			_ => builder.GetCompressedStringOrNull(propertyName),
		};


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> with <see cref="DateTime"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasDateTimeProperty(string propertyName)
		{
			SetupPropertyMap();
			return dateTimePropertyNameSet.Contains(propertyName);
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
		public static bool HasProperty(string propertyName) => propertyIndices.ContainsKey(propertyName);


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> with <see cref="string"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasStringProperty(string propertyName)
		{
			SetupPropertyMap();
			return stringPropertyNameSet.Contains(propertyName);
		}


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> with <see cref="TimeSpan"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasTimeSpanProperty(string propertyName)
		{
			SetupPropertyMap();
			return timeSpanPropertyNameSet.Contains(propertyName);
		}


		/// <summary>
		/// Get unique incremental ID of this instance.
		/// </summary>
		public long Id { get; }


		/// <summary>
		/// Get level.
		/// </summary>
		public LogLevel Level { get => (LogLevel)(this.GetProperty(PropertyName.Level) ?? LogLevel.Undefined); }


		/// <summary>
		/// Get line number.
		/// </summary>
		public int? LineNumber { get => (int?)this.GetProperty(PropertyName.LineNumber); }


		/// <summary>
		/// Get size of memory usage by the instance in bytes.
		/// </summary>
		public long MemorySize { get; }


		/// <summary>
		/// Get message.
		/// </summary>
		public string? Message { get => this.GetProperty(PropertyName.Message)?.ToString(); }


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId { get => (int?)this.GetProperty(PropertyName.ProcessId); }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public string? ProcessName { get => this.GetProperty(PropertyName.ProcessName)?.ToString(); }


		/// <summary>
		/// Get list of log properties exported by <see cref="Log"/>.
		/// </summary>
		public static IList<string> PropertyNames { get => propertyNames; }


		/// <summary>
		/// Get the timestamp of this log was read.
		/// </summary>
		public DateTime ReadTime => (DateTime)this.GetProperty(PropertyName.ReadTime).AsNonNull();


		// Setup property map.
		static void SetupPropertyMap()
		{
			if (!isPropertyMapReady)
			{
				var logType = typeof(Log);
				lock (logType)
				{
					if (!isPropertyMapReady)
					{
						foreach (var propertyName in propertyNames)
						{
							var propertyInfo = logType.GetProperty(propertyName);
							if (propertyInfo == null)
								continue;
							propertyMap[propertyInfo.Name] = propertyInfo;
							if (propertyInfo.PropertyType == typeof(string))
								stringPropertyNameSet.Add(propertyName);
							else if (propertyInfo.PropertyType == typeof(DateTime?) || propertyInfo.PropertyType == typeof(DateTime))
								dateTimePropertyNameSet.Add(propertyName);
							else if (propertyInfo.PropertyType == typeof(TimeSpan?) || propertyInfo.PropertyType == typeof(TimeSpan))
								timeSpanPropertyNameSet.Add(propertyName);
						}
						isPropertyMapReady = true;
					}
				}
			}
		}


		/// <summary>
		/// Get name of source which generates log.
		/// </summary>
		public string? SourceName { get => this.GetProperty(PropertyName.SourceName)?.ToString(); }


		/// <summary>
		/// Get summary of log.
		/// </summary>
		public string? Summary { get => this.GetProperty(PropertyName.Summary)?.ToString(); }


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public string? Tags { get => this.GetProperty(PropertyName.Tags)?.ToString(); }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId { get => (int?)this.GetProperty(PropertyName.ThreadId); }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public string? ThreadName { get => this.GetProperty(PropertyName.ThreadName)?.ToString(); }


		/// <summary>
		/// Get time span.
		/// </summary>
		public TimeSpan? TimeSpan { get => (TimeSpan?)this.GetProperty(PropertyName.TimeSpan); }


		/// <summary>
		/// Get timestamp.
		/// </summary>
		public DateTime? Timestamp { get => (DateTime?)this.GetProperty(PropertyName.Timestamp); }


		/// <summary>
		/// Get title of log.
		/// </summary>
		public string? Title { get => this.GetProperty(PropertyName.Title)?.ToString(); }


		/// Try getting the earliest/latest timestamp from <see cref="BeginningTimestamp"/>, <see cref="EndingTimestamp"/> and <see cref="Timestamp"/>.
		/// </summary>
		/// <param name="earliestTimestamp">The earliest timestamp.</param>
		/// <param name="latestTimestamp">The latest timestamp.</param>
		/// <returns>True if the earliest/latest timestamp are valid.</returns>
		public unsafe bool TryGetEarliestAndLatestTimestamp([NotNullWhen(true)] out DateTime? earliestTimestamp, [NotNullWhen(true)] out DateTime? latestTimestamp)
		{
			var timestamps = stackalloc DateTime?[]
			{
				this.BeginningTimestamp,
				this.EndingTimestamp,
				this.Timestamp
			};
			earliestTimestamp = default;
			latestTimestamp = default;
			for (var i = 2; i >= 0; --i)
			{
				var timestamp = timestamps[i];
				if (!timestamp.HasValue)
					continue;
				if (!earliestTimestamp.HasValue || earliestTimestamp > timestamp)
					earliestTimestamp = timestamp;
				if (!latestTimestamp.HasValue || latestTimestamp < timestamp)
					latestTimestamp = timestamp;
			}
			return earliestTimestamp.HasValue;
		}


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
			var propertyIndex = propertyNames.BinarySearch(propertyName);
			if (propertyIndex < 0)
			{
				value = default;
				return false;
			}
			var valueIndex = this.propertyValueIndices[propertyIndex];
			if (valueIndex <= 0)
			{
				value = default;
				return false;
			}
			var rawValue = this.propertyValues[valueIndex - 1];
			if (rawValue == null || !typeof(T).IsAssignableFrom(rawValue.GetType()))
			{
				value = default;
				return false;
			}
			value = (T)rawValue;
			return true;
		}
#pragma warning restore CS8601


		/// Try getting the smallest/largest time span from <see cref="BeginningTimeSpan"/>, <see cref="EndingTimeSpan"/> and <see cref="TimeSpan"/>.
		/// </summary>
		/// <param name="smallestTimeSpan">The smallest time span.</param>
		/// <param name="largestTimeSpan">The largest time span.</param>
		/// <returns>True if the smallest/largest time span are valid.</returns>
		public unsafe bool TryGetSmallestAndLargestTimeSpan([NotNullWhen(true)] out TimeSpan? smallestTimeSpan, [NotNullWhen(true)] out TimeSpan? largestTimeSpan)
		{
			var timeSpans = stackalloc TimeSpan?[]
			{
				this.BeginningTimeSpan,
				this.EndingTimeSpan,
				this.TimeSpan
			};
			smallestTimeSpan = default;
			largestTimeSpan = default;
			for (var i = 2; i >= 0; --i)
			{
				var timeSpan = timeSpans[i];
				if (!timeSpan.HasValue)
					continue;
				if (!smallestTimeSpan.HasValue || smallestTimeSpan > timeSpan)
					smallestTimeSpan = timeSpan;
				if (!largestTimeSpan.HasValue || largestTimeSpan < timeSpan)
					largestTimeSpan = timeSpan;
			}
			return smallestTimeSpan.HasValue;
		}


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public string? UserId { get => this.GetProperty(PropertyName.UserId)?.ToString(); }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public string? UserName { get => this.GetProperty(PropertyName.UserName)?.ToString(); }
	}


	/// <summary>
	/// Delegate to get value of string property of log.
	/// </summary>
	/// <param name="log">Log.</param>
	/// <param name="buffer">Buffer.</param>
	/// <param name="offset">Offset in buffer to put first character.</param>
	/// <returns>Number of characters in original string, or 1's complement of number of characters if size of buffer is insufficient.</returns>
	delegate int LogStringPropertyGetter(Log log, Span<char> buffer, int offset = 0);
}
