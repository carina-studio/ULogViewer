using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
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
		static readonly HashSet<string> dateTimePropertyNameSet = new HashSet<string>();
		static readonly int instanceFieldMemorySize;
		static volatile bool isPropertyMapReady;
		static long nextId = 0;
		static readonly Dictionary<string, int> propertyIndices = new Dictionary<string, int>();
		static readonly Dictionary<string, PropertyInfo> propertyMap = new Dictionary<string, PropertyInfo>();
		static readonly IList<string> propertyNames = Enum.GetValues<PropertyName>().Let(propertyNames =>
		{
			var propertyCount = propertyNames.Length;
			return new List<string>(propertyCount).Also(it =>
			{
				for (var i = 0; i < propertyCount; ++i)
					it.Add(propertyNames[i].ToString());
			}).AsReadOnly();
		});
		static readonly HashSet<string> stringPropertyNameSet = new HashSet<string>();
		static readonly HashSet<string> timeSpanPropertyNameSet = new HashSet<string>();


		// Fields.
		readonly byte[] propertyValueIndices = new byte[propertyNames.Count];
		readonly object?[] propertyValues;


		// Static initializer.
		static Log()
		{
			for (var i = propertyNames.Count - 1; i >= 0; --i)
				propertyIndices[propertyNames[i]] = i;
			foreach (var field in typeof(Log).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
			{
				var type = field.FieldType;
				if (!type.IsValueType)
					instanceFieldMemorySize += IntPtr.Size;
				else if (type == typeof(byte))
					instanceFieldMemorySize += 1;
				else if (type == typeof(short))
					instanceFieldMemorySize += 2;
				else if (type == typeof(int))
					instanceFieldMemorySize += 4;
				else if (type == typeof(long))
					instanceFieldMemorySize += 8;
				else
					throw new NotSupportedException();
			}
		}


		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			// prepare
			var propertyCount = builder.PropertyCount;
			var propertyValueIndices = this.propertyValueIndices;
			var propertyValues = new object?[propertyCount];
			var index = 0;
			long propertyMemorySize = 0;
			foreach (var propertyName in builder.PropertyNames)
			{
				var value = GetPropertyFromBuilder(builder, propertyName);
				if (value == null)
					continue;
				if (!propertyIndices.TryGetValue(propertyName, out var propertyIndex))
					continue;
				propertyValueIndices[propertyIndex] = (byte)(index + 1);
				propertyValues[index++] = value;
				if (value is CompressedString compressedString)
					propertyMemorySize += compressedString.Size;
				else if (value is string str)
					propertyMemorySize += (str.Length << 1);
				else if (value is int || value is Enum)
					propertyMemorySize += 4;
				else if (value is DateTime || value is TimeSpan)
					propertyMemorySize += 8;
				else
					throw new NotSupportedException();
			}
			this.propertyValues = propertyValues;

			// get ID
			this.Id = Interlocked.Increment(ref nextId);

			// calculate memory size
			this.MemorySize = instanceFieldMemorySize + propertyMemorySize + this.propertyValueIndices.Length + (this.propertyValues.Length * IntPtr.Size);
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
		/// Select timestamp which is the earliest one.
		/// </summary>
		/// <returns>Selected timestamp.</returns>
		public DateTime? SelectEarliestTimestamp()
		{
			var timestamp = this.Timestamp;
			var beginningTimestamp = this.BeginningTimestamp;
			if (timestamp != null && beginningTimestamp != null)
			{
				if (timestamp.Value <= beginningTimestamp.Value)
					return timestamp;
				return beginningTimestamp;
			}
			var endingTimestamp = this.EndingTimestamp;
			if (endingTimestamp != null)
			{
				if (beginningTimestamp != null)
				{
					if (beginningTimestamp.Value <= endingTimestamp.Value)
						return beginningTimestamp;
				}
				else
				{
					if (timestamp.GetValueOrDefault() <= endingTimestamp.Value)
						return timestamp;
				}
				return endingTimestamp;
			}
			else if (beginningTimestamp != null)
				return beginningTimestamp;
			return timestamp;
		}


		/// <summary>
		/// Select timestamp which is the latest one.
		/// </summary>
		/// <returns>Selected timestamp.</returns>
		public DateTime? SelectLatestTimestamp()
		{
			var timestamp = this.Timestamp;
			var endingTimestamp = this.EndingTimestamp;
			if (timestamp != null && endingTimestamp != null)
			{
				if (timestamp.Value >= endingTimestamp.Value)
					return timestamp;
				return endingTimestamp;
			}
			var beginningTimestamp = this.BeginningTimestamp;
			if (beginningTimestamp != null)
			{
				if (endingTimestamp != null)
				{
					if (endingTimestamp.Value >= beginningTimestamp.Value)
						return endingTimestamp;
				}
				else
				{
					if (timestamp.GetValueOrDefault() >= beginningTimestamp.Value)
						return timestamp;
				}
				return beginningTimestamp;
			}
			else if (endingTimestamp != null)
				return endingTimestamp;
			return timestamp;
		}


		/// <summary>
		/// Select time span which is the maximum one.
		/// </summary>
		/// <returns>Selected time span.</returns>
		public TimeSpan? SelectMaxTimeSpan()
		{
			var timeSpan = this.TimeSpan;
			var endingTimeSpan = this.EndingTimeSpan;
			if (timeSpan != null && endingTimeSpan != null)
			{
				if (timeSpan.Value >= endingTimeSpan.Value)
					return timeSpan;
				return endingTimeSpan;
			}
			var beginningTimeSpan = this.BeginningTimeSpan;
			if (beginningTimeSpan != null)
			{
				if (endingTimeSpan != null)
				{
					if (endingTimeSpan.Value >= beginningTimeSpan.Value)
						return endingTimeSpan;
				}
				else
				{
					if (timeSpan.GetValueOrDefault() >= beginningTimeSpan.Value)
						return timeSpan;
				}
				return beginningTimeSpan;
			}
			else if (endingTimeSpan != null)
				return endingTimeSpan;
			return timeSpan;
		}


		/// <summary>
		/// Select time span which is the minimum one.
		/// </summary>
		/// <returns>Selected time span.</returns>
		public TimeSpan? SelectMinTimeSpan()
		{
			var timeSpan = this.TimeSpan;
			var beginningTimeSpan = this.BeginningTimeSpan;
			if (timeSpan != null && beginningTimeSpan != null)
			{
				if (timeSpan.Value <= beginningTimeSpan.Value)
					return timeSpan;
				return beginningTimeSpan;
			}
			var endingTimeSpan = this.EndingTimeSpan;
			if (endingTimeSpan != null)
			{
				if (beginningTimeSpan != null)
				{
					if (beginningTimeSpan.Value <= endingTimeSpan.Value)
						return beginningTimeSpan;
				}
				else
				{
					if (timeSpan.GetValueOrDefault() <= endingTimeSpan.Value)
						return timeSpan;
				}
				return endingTimeSpan;
			}
			else if (beginningTimeSpan != null)
				return beginningTimeSpan;
			return timeSpan;
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
						var logType = typeof(Log);
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


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public string? UserId { get => this.GetProperty(PropertyName.UserId)?.ToString(); }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public string? UserName { get => this.GetProperty(PropertyName.UserName)?.ToString(); }
	}
}
