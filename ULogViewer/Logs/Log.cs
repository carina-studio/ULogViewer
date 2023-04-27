using CarinaStudio.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Reflection;
using CarinaStudio.ULogViewer.Text;

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
		public const int ExtraCapacity = 20;


		// Property definitions.
		enum PropertyName : byte
		{
			None,
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
			Extra11,
			Extra12,
			Extra13,
			Extra14,
			Extra15,
			Extra16,
			Extra17,
			Extra18,
			Extra19,
			Extra2,
			Extra20,
			Extra3,
			Extra4,
			Extra5,
			Extra6,
			Extra7,
			Extra8,
			Extra9,
			FileName,
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
		static readonly IList<string> allPropertyNames = Enum.GetValues<PropertyName>().Let(propertyNames =>
		{
			var propertyCount = propertyNames.Length;
			return new List<string>(propertyCount).Also(it =>
			{
				for (var i = 0; i < propertyCount; ++i)
				{
					if (propertyNames[i] == PropertyName.None)
						continue;
					it.Add(propertyNames[i].ToString());
				}
				it.Add(nameof(Level));
				it.Add(nameof(ReadTime));
			}).AsReadOnly();
		});
		static readonly long baseMemorySize = Memory.EstimateInstanceSize<Log>();
		static readonly HashSet<string> dateTimePropertyNameSet = new();
		static volatile bool isPropertyMapReady;
		static readonly HashSet<string> multiLineStringPropertyNames = new()
		{
			nameof(Message),
			nameof(Summary),
		};
		static long nextId;
		static readonly Dictionary<string, PropertyInfo> propertyMap = new();
		static readonly HashSet<string> stringPropertyNameSet = new();
		static readonly HashSet<string> timeSpanPropertyNameSet = new();


		// Fields.
		readonly LogLevel level;
		readonly uint memorySize;
		readonly byte[] propertyNames;
		readonly object?[] propertyValues;
		readonly DateTime readTime;
		
		
		// Static initializer.
		static Log()
		{
			for (var i = ExtraCapacity; i > 0; --i)
				multiLineStringPropertyNames.Add($"Extra{i}");
		}


		/// <summary>
		/// Initialize new <see cref="Log"/> instance.
		/// </summary>
		/// <param name="builder"><see cref="LogBuilder"/>.</param>
		internal Log(LogBuilder builder)
		{
			// prepare
			var propertyCount = builder.PropertyCount;
			var level = builder.GetEnumOrNull<LogLevel>(nameof(Level));
			if (level.HasValue)
				--propertyCount;
			var propertyNames = new byte[propertyCount];
			var propertyValues = new object?[propertyCount];
			var index = 0;
			var propertyMemorySize = 0L;
			foreach (var propertyName in builder.PropertyNames)
			{
				if (propertyName == nameof(Level) || !Enum.TryParse<PropertyName>(propertyName, out var name))
					continue;
				propertyNames[index++] = (byte)name;
			}
			Array.Sort(propertyNames);
			for (var i = propertyNames.Length - 1; i >= 0; --i)
			{
				var propertyName = (PropertyName)propertyNames[i];
				var value = GetPropertyFromBuilder(builder, propertyName.ToString(), out var fromCache);
				if (value is null)
					continue;
				propertyValues[i] = value;
				if (!fromCache)
				{
					if (value is IStringSource stringSource)
						propertyMemorySize += stringSource.ByteCount;
					else if (value is string str)
						propertyMemorySize += Memory.EstimateInstanceSize(typeof(string), str.Length);
					else
						propertyMemorySize += Memory.EstimateInstanceSize(value);
				}
			}
			this.propertyNames = propertyNames;
			this.propertyValues = propertyValues;

			// get ID
			this.Id = Interlocked.Increment(ref nextId);

			// setup level
			this.level = level ?? LogLevel.Undefined;

			// setup reading time
			this.readTime = DateTime.Now;

			// calculate memory size
			this.memorySize = (ushort)(baseMemorySize 
			                           + propertyMemorySize 
			                           + Memory.EstimateArrayInstanceSize<byte>(propertyNames.Length) 
			                           + Memory.EstimateArrayInstanceSize(IntPtr.Size, this.propertyValues.Length));
		}


		/// <summary>
		/// Get beginning time span.
		/// </summary>
		public TimeSpan? BeginningTimeSpan => (TimeSpan?)this.GetProperty(PropertyName.BeginningTimeSpan);


		/// <summary>
		/// Get beginning timestamp.
		/// </summary>
		public DateTime? BeginningTimestamp => (DateTime?)this.GetProperty(PropertyName.BeginningTimestamp);


		/// <summary>
		/// Get category of log.
		/// </summary>
		public IStringSource? Category => this.GetProperty(PropertyName.Category) as IStringSource;


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
			return (_ => default);
		}
#pragma warning restore CS8603
#pragma warning restore CS8600


		/// <summary>
		/// Get ID of device which generates log.
		/// </summary>
		public IStringSource? DeviceId => this.GetProperty(PropertyName.DeviceId) as IStringSource;


		/// <summary>
		/// Get name of device which generates log.
		/// </summary>
		public IStringSource? DeviceName => this.GetProperty(PropertyName.DeviceName) as IStringSource;


		/// <summary>
		/// Get ending time span.
		/// </summary>
		public TimeSpan? EndingTimeSpan => (TimeSpan?)this.GetProperty(PropertyName.EndingTimeSpan);


		/// <summary>
		/// Get ending timestamp.
		/// </summary>
		public DateTime? EndingTimestamp => (DateTime?)this.GetProperty(PropertyName.EndingTimestamp);


		/// <summary>
		/// Get event of log.
		/// </summary>
		public IStringSource? Event => this.GetProperty(PropertyName.Event) as IStringSource;


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public IStringSource? Extra1 => this.GetProperty(PropertyName.Extra1) as IStringSource;


		/// <summary>
		/// Get 10th extra data of log.
		/// </summary>
		public IStringSource? Extra10 => this.GetProperty(PropertyName.Extra10) as IStringSource;
		
		
		/// <summary>
		/// Get 11st extra data of log.
		/// </summary>
		public IStringSource? Extra11 => this.GetProperty(PropertyName.Extra11) as IStringSource;
		
		
		/// <summary>
		/// Get 12nd extra data of log.
		/// </summary>
		public IStringSource? Extra12 => this.GetProperty(PropertyName.Extra12) as IStringSource;
		
		
		/// <summary>
		/// Get 13rd extra data of log.
		/// </summary>
		public IStringSource? Extra13 => this.GetProperty(PropertyName.Extra13) as IStringSource;
		
		
		/// <summary>
		/// Get 14th extra data of log.
		/// </summary>
		public IStringSource? Extra14 => this.GetProperty(PropertyName.Extra14) as IStringSource;
		
		
		/// <summary>
		/// Get 15th extra data of log.
		/// </summary>
		public IStringSource? Extra15 => this.GetProperty(PropertyName.Extra15) as IStringSource;
		
		
		/// <summary>
		/// Get 16th extra data of log.
		/// </summary>
		public IStringSource? Extra16 => this.GetProperty(PropertyName.Extra16) as IStringSource;
		
		
		/// <summary>
		/// Get 17th extra data of log.
		/// </summary>
		public IStringSource? Extra17 => this.GetProperty(PropertyName.Extra17) as IStringSource;
		
		
		/// <summary>
		/// Get 18th extra data of log.
		/// </summary>
		public IStringSource? Extra18 => this.GetProperty(PropertyName.Extra18) as IStringSource;
		
		
		/// <summary>
		/// Get 19th extra data of log.
		/// </summary>
		public IStringSource? Extra19 => this.GetProperty(PropertyName.Extra19) as IStringSource;


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public IStringSource? Extra2 => this.GetProperty(PropertyName.Extra2) as IStringSource;
		
		
		/// <summary>
		/// Get 20th extra data of log.
		/// </summary>
		public IStringSource? Extra20 => this.GetProperty(PropertyName.Extra20) as IStringSource;


		/// <summary>
		/// Get 3rd extra data of log.
		/// </summary>
		public IStringSource? Extra3 => this.GetProperty(PropertyName.Extra3) as IStringSource;


		/// <summary>
		/// Get 4th extra data of log.
		/// </summary>
		public IStringSource? Extra4 => this.GetProperty(PropertyName.Extra4) as IStringSource;


		/// <summary>
		/// Get 5th extra data of log.
		/// </summary>
		public IStringSource? Extra5 => this.GetProperty(PropertyName.Extra5) as IStringSource;


		/// <summary>
		/// Get 6th extra data of log.
		/// </summary>
		public IStringSource? Extra6 => this.GetProperty(PropertyName.Extra6) as IStringSource;


		/// <summary>
		/// Get 7th extra data of log.
		/// </summary>
		public IStringSource? Extra7 => this.GetProperty(PropertyName.Extra7) as IStringSource;


		/// <summary>
		/// Get 8th extra data of log.
		/// </summary>
		public IStringSource? Extra8 => this.GetProperty(PropertyName.Extra8) as IStringSource;


		/// <summary>
		/// Get 9th extra data of log.
		/// </summary>
		public IStringSource? Extra9 => this.GetProperty(PropertyName.Extra9) as IStringSource;


		/// <summary>
		/// Get name of file which log read from.
		/// </summary>
		public IStringSource? FileName => this.GetProperty(PropertyName.FileName) as IStringSource;


		// Get property.
		object? GetProperty(PropertyName propertyName)
		{
			int index = Array.BinarySearch(this.propertyNames, (byte)propertyName);
			if (index >= 0)
				return this.propertyValues[index];
			return null;
		}


		// Get property from log builder.
		static object? GetPropertyFromBuilder(LogBuilder builder, string propertyName, out bool fromCache)
		{
			fromCache = false;
			return propertyName switch
			{
				nameof(BeginningTimeSpan)
					or nameof(EndingTimeSpan)
					or nameof(TimeSpan) => builder.GetTimeSpanOrNull(propertyName),
				nameof(BeginningTimestamp)
					or nameof(EndingTimestamp)
					or nameof(Timestamp) => builder.GetDateTimeOrNull(propertyName),
				nameof(Level) => builder.GetEnumOrNull<LogLevel>(propertyName) ?? LogLevel.Undefined,
				nameof(LineNumber)
					or nameof(ProcessId)
					or nameof(ThreadId) => builder.GetInt32OrNull(propertyName),
				_ => builder.GetStringOrNull(propertyName, out fromCache),
			};
		}


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
		public static bool HasMultiLineStringProperty(string propertyName) =>
			multiLineStringPropertyNames.Contains(propertyName);


		/// <summary>
		/// Check whether given log property is exported by <see cref="Log"/> or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasProperty(string propertyName) => 
			propertyName == nameof(Level)
			|| propertyName == nameof(ReadTime)
			|| (Enum.TryParse<PropertyName>(propertyName, out var name) && name != PropertyName.None);


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
		public LogLevel Level => this.level;


		/// <summary>
		/// Get line number.
		/// </summary>
		public int? LineNumber => (int?)this.GetProperty(PropertyName.LineNumber);


		/// <summary>
		/// Get size of memory usage by the instance in bytes.
		/// </summary>
		public long MemorySize => this.memorySize;


		/// <summary>
		/// Get message.
		/// </summary>
		public IStringSource? Message => this.GetProperty(PropertyName.Message) as IStringSource;


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId => (int?)this.GetProperty(PropertyName.ProcessId);


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public IStringSource? ProcessName => this.GetProperty(PropertyName.ProcessName) as IStringSource;


		/// <summary>
		/// Get list of log properties exported by <see cref="Log"/>.
		/// </summary>
		public static IList<string> PropertyNames => allPropertyNames;


		/// <summary>
		/// Get the timestamp of this log was read.
		/// </summary>
		public DateTime ReadTime => this.readTime;


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
						foreach (var propertyName in allPropertyNames)
						{
							var propertyInfo = logType.GetProperty(propertyName);
							if (propertyInfo == null)
								continue;
							propertyMap[propertyInfo.Name] = propertyInfo;
							if (propertyInfo.PropertyType == typeof(IStringSource))
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
		public IStringSource? SourceName => this.GetProperty(PropertyName.SourceName) as IStringSource;


		/// <summary>
		/// Get summary of log.
		/// </summary>
		public IStringSource? Summary => this.GetProperty(PropertyName.Summary) as IStringSource;


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public IStringSource? Tags => this.GetProperty(PropertyName.Tags) as IStringSource;


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId => (int?)this.GetProperty(PropertyName.ThreadId);


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public IStringSource? ThreadName => this.GetProperty(PropertyName.ThreadName) as IStringSource;


		/// <summary>
		/// Get time span.
		/// </summary>
		public TimeSpan? TimeSpan => (TimeSpan?)this.GetProperty(PropertyName.TimeSpan);


		/// <summary>
		/// Get timestamp.
		/// </summary>
		public DateTime? Timestamp => (DateTime?)this.GetProperty(PropertyName.Timestamp);


		/// <summary>
		/// Get title of log.
		/// </summary>
		public IStringSource? Title => this.GetProperty(PropertyName.Title) as IStringSource;


		/// <summary>
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
			if (!Enum.TryParse<PropertyName>(propertyName, out var name) || name == PropertyName.None)
			{
				value = default;
				return false;
			}
			var index = Array.BinarySearch(this.propertyNames, (byte)name);
			if (index < 0)
			{
				value = default;
				return false;
			}
			var rawValue = this.propertyValues[index];
			if (rawValue is IStringSource stringSource)
			{
				if (stringSource is T stringSourceT)
				{
					value = stringSourceT;
					return true;
				}
				if (typeof(T) == typeof(string))
				{
					value = (T)(object)stringSource.ToString();
					return true;
				}
			}
			if (rawValue is T valueT)
			{
				value = valueT;
				return true;
			}
			value = default;
			return false;
		}
#pragma warning restore CS8600
#pragma warning restore CS8601


		/// <summary>
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
		public IStringSource? UserId => this.GetProperty(PropertyName.UserId) as IStringSource;


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public IStringSource? UserName => this.GetProperty(PropertyName.UserName) as IStringSource;
	}
}
