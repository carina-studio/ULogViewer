using CarinaStudio.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Reflection;
using CarinaStudio.ULogViewer.Text;
using System.Buffers.Binary;

namespace CarinaStudio.ULogViewer.Logs;

/// <summary>
/// Represents a single log.
/// </summary>
unsafe class Log
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
	
	
	// Type of property value.
	enum PropertyType
	{
		DateTime,
		Int32,
		String,
		TimeSpan,
	}
	
	
	// Constants (Data offset)
	const int MemorySizeDataOffset = 0;
	const int ReadTimeDataOffset = 4;
	const int LevelDataOffset = 12;
	const int ObjectPropertyCountDataOffset = 13;
	const int ValuePropertyCountDataOffset = 14;
	const int ObjectPropertyNamesDataOffset = 15;


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
	static readonly HashSet<string> int32PropertyNameSet = new();
	static volatile bool isPropertyMapReady;
	static readonly HashSet<string> multiLineStringPropertyNames =
	[
		nameof(Message),
		nameof(Summary)
	];
	static long nextId;
	static readonly Dictionary<string, PropertyInfo> propertyMap = new();
	static readonly PropertyType[] propertyTypeMap =
	[
		default, // None
		PropertyType.TimeSpan,
		PropertyType.DateTime,
		PropertyType.String, // Category
		PropertyType.String,
		PropertyType.String,
		PropertyType.TimeSpan,
		PropertyType.DateTime,
		PropertyType.String,
		PropertyType.String, // Extra1
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String, // Extra9
		PropertyType.String, // FileName
		PropertyType.Int32, // LineNumber
		PropertyType.String,
		PropertyType.Int32, // ProcessId
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String,
		PropertyType.Int32, // ThreadId
		PropertyType.String,
		PropertyType.TimeSpan,
		PropertyType.DateTime,
		PropertyType.String,
		PropertyType.String,
		PropertyType.String
	];
	static readonly delegate*<ReadOnlySpan<byte>, int> readInt32Function;
	static readonly delegate*<ReadOnlySpan<byte>, long> readInt64Function;
	[ThreadStatic] 
	static byte[]? sharedObjectPropertyNames;
	[ThreadStatic] 
	static object?[]? sharedValueProperties;
	[ThreadStatic] 
	static byte[]? sharedValuePropertyNames;
	[ThreadStatic] 
	static PropertyType[]? sharedValuePropertyTypes;
	static readonly HashSet<string> stringPropertyNameSet = new();
	static readonly HashSet<string> timeSpanPropertyNameSet = new();
	static readonly delegate*<Span<byte>, int, void> writeInt32Function;
	static readonly delegate*<Span<byte>, long, void> writeInt64Function;


	// Fields.
	/*
	 * Data layout:
	 * [0, 4) Memory size
	 * [4, 12) Reading time
	 * [12, 13) Level
	 * [13, 14) Number of object properties = N
	 * [14, 15) Number of value properties = M
	 * [15, 15 + N) Object property names
	 * [15 + N, 15 + N + M) Value property names
	 * [15 + N + M, ...) Value properties
	 */
	readonly byte[] data;
	readonly object?[]? objectProperties;
	
	
	// Static initializer.
	static Log()
	{
		for (var i = ExtraCapacity; i > 0; --i)
			multiLineStringPropertyNames.Add($"Extra{i}");
		var n = 1;
		if (*(byte*)&n == 1) // BE
		{
			readInt32Function = &BinaryPrimitives.ReadInt32BigEndian;
			readInt64Function = &BinaryPrimitives.ReadInt64BigEndian;
			writeInt32Function = &BinaryPrimitives.WriteInt32BigEndian;
			writeInt64Function = &BinaryPrimitives.WriteInt64BigEndian;
		}
		else // LE
		{
			readInt32Function = &BinaryPrimitives.ReadInt32LittleEndian;
			readInt64Function = &BinaryPrimitives.ReadInt64LittleEndian;
			writeInt32Function = &BinaryPrimitives.WriteInt32LittleEndian;
			writeInt64Function = &BinaryPrimitives.WriteInt64LittleEndian;
		}
	}


	/// <summary>
	/// Initialize new <see cref="Log"/> instance.
	/// </summary>
	/// <param name="builder"><see cref="LogBuilder"/>.</param>
	internal Log(LogBuilder builder)
	{
		// collect properties
		sharedObjectPropertyNames ??= new byte[propertyTypeMap.Length];
		sharedValuePropertyNames ??= new byte[propertyTypeMap.Length];
		sharedValuePropertyTypes ??= new PropertyType[propertyTypeMap.Length];
		sharedValueProperties ??= new object?[propertyTypeMap.Length];
		var objectPropertyCount = 0;
		var objectPropertyNames = sharedObjectPropertyNames;
		var valuePropertyCount = 0;
		var valuePropertyNames = sharedValuePropertyNames;
		var valuePropertyTypes = sharedValuePropertyTypes;
		var valueProperties = sharedValueProperties;
		var valuePropertiesByteCount = 0;
		foreach (var propertyName in builder.PropertyNames)
		{
			if (propertyName == nameof(Level) || !Enum.TryParse<PropertyName>(propertyName, out var name))
				continue;
			var nameValue = (byte)name;
			var type = propertyTypeMap[nameValue];
			switch (type)
			{
				case PropertyType.DateTime:
				{
					var value = builder.GetDateTimeOrNull(propertyName);
					if (value.HasValue)
					{
						valueProperties[nameValue] = value.Value;
						valuePropertyNames[valuePropertyCount] = nameValue;
						valuePropertyTypes[valuePropertyCount++] = type;
						valuePropertiesByteCount += 8;
					}
					break;
				}
				case PropertyType.Int32:
				{
					var value = builder.GetInt32OrNull(propertyName);
					if (value.HasValue)
					{
						valueProperties[nameValue] = value.Value;
						valuePropertyNames[valuePropertyCount] = nameValue;
						valuePropertyTypes[valuePropertyCount++] = type;
						valuePropertiesByteCount += 4;
					}
					break;
				}
				case PropertyType.String:
					objectPropertyNames[objectPropertyCount++] = (byte)name;
					break;
				case PropertyType.TimeSpan:
				{
					var value = builder.GetTimeSpanOrNull(propertyName);
					if (value.HasValue)
					{
						valueProperties[nameValue] = value.Value;
						valuePropertyNames[valuePropertyCount] = nameValue;
						valuePropertyTypes[valuePropertyCount++] = type;
						valuePropertiesByteCount += 8;
					}
					break;
				}
				default:
					throw new NotSupportedException();
			}
		}
		
		// allocate data buffer
		var data = new byte[ValuePropertyCountDataOffset + 1 + objectPropertyCount + valuePropertyCount + valuePropertiesByteCount];
		this.data = data;
		
		// copy object properties
		var objectPropertiesByteCount = 0L;
		if (objectPropertyCount > 0)
		{
			var objectProperties = new object?[objectPropertyCount];
			this.objectProperties = objectProperties;
			data[ObjectPropertyCountDataOffset] = (byte)objectPropertyCount;
			Array.Sort(objectPropertyNames, 0, objectPropertyCount);
			Array.Copy(objectPropertyNames, 0, data, ObjectPropertyNamesDataOffset, objectPropertyCount);
			for (var i = 0; i < objectPropertyCount; ++i)
			{
				var propertyName = ((PropertyName)objectPropertyNames[i]).ToString();
				switch (propertyTypeMap[objectPropertyNames[i]])
				{
					case PropertyType.String:
					{
						var s = builder.GetStringOrNull(propertyName, out var fromCache);
						objectProperties[i] = s;
						if (!fromCache && s is not null)
							objectPropertiesByteCount += s.ByteCount;
						break;
					}
					default:
						throw new NotSupportedException();
				}
			}
		}

		// copy value properties
		if (valuePropertyCount > 0)
		{
			var valuePropertyNamesOffset = ValuePropertyCountDataOffset + 1 + objectPropertyCount;
			var valuePropertyOffset = valuePropertyNamesOffset + valuePropertyCount;
			data[ValuePropertyCountDataOffset] = (byte)valuePropertyCount;
			Array.Sort(valuePropertyNames, 0, valuePropertyCount);
			Array.Copy(valuePropertyNames, 0, data, valuePropertyNamesOffset, valuePropertyCount);
			for (var i = 0; i < valuePropertyCount; ++i)
			{
				switch (propertyTypeMap[valuePropertyNames[i]])
				{
					case PropertyType.DateTime:
					{
						var value = (DateTime)valueProperties[valuePropertyNames[i]]!;
						writeInt64Function(data.AsSpan(valuePropertyOffset), value.ToBinary());
						valuePropertyOffset += 8;
						break;
					}
					case PropertyType.Int32:
					{
						var value = (int)valueProperties[valuePropertyNames[i]]!;
						writeInt32Function(data.AsSpan(valuePropertyOffset), value);
						valuePropertyOffset += 4;
						break;
					}
					case PropertyType.TimeSpan:
					{
						var value = (TimeSpan)valueProperties[valuePropertyNames[i]]!;
						writeInt64Function(data.AsSpan(valuePropertyOffset), value.Ticks);
						valuePropertyOffset += 8;
						break;
					}
					default:
						throw new NotSupportedException();
				}
			}
		}
		
		// get ID
		this.Id = Interlocked.Increment(ref nextId);

		// setup level
		var level = builder.GetEnumOrNull<LogLevel>(nameof(Level));
		data[LevelDataOffset] = (byte)(level ?? LogLevel.Undefined);

		// setup reading time
		writeInt64Function(data.AsSpan(ReadTimeDataOffset), DateTime.Now.ToBinary());

		// calculate memory size
		writeInt32Function(data.AsSpan(MemorySizeDataOffset), (int)(baseMemorySize 
		                           + objectPropertiesByteCount 
		                           + Memory.EstimateArrayInstanceSize<byte>(data.Length) 
		                           + (this.objectProperties is null ? 0 : Memory.EstimateArrayInstanceSize(IntPtr.Size, this.objectProperties.Length))));
	}


	/// <summary>
	/// Get beginning time span.
	/// </summary>
	public TimeSpan? BeginningTimeSpan => this.GetValueProperty<TimeSpan>(PropertyName.BeginningTimeSpan);


	/// <summary>
	/// Get beginning timestamp.
	/// </summary>
	public DateTime? BeginningTimestamp => this.GetValueProperty<DateTime>(PropertyName.BeginningTimestamp);


	/// <summary>
	/// Get category of log.
	/// </summary>
	public IStringSource? Category => this.GetObjectProperty<IStringSource>(PropertyName.Category);


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
	public IStringSource? DeviceId => this.GetObjectProperty<IStringSource>(PropertyName.DeviceId);


	/// <summary>
	/// Get name of device which generates log.
	/// </summary>
	public IStringSource? DeviceName => this.GetObjectProperty<IStringSource>(PropertyName.DeviceName);


	/// <summary>
	/// Get ending time span.
	/// </summary>
	public TimeSpan? EndingTimeSpan => this.GetValueProperty<TimeSpan>(PropertyName.EndingTimeSpan);


	/// <summary>
	/// Get ending timestamp.
	/// </summary>
	public DateTime? EndingTimestamp => this.GetValueProperty<DateTime>(PropertyName.EndingTimestamp);


	/// <summary>
	/// Get event of log.
	/// </summary>
	public IStringSource? Event => this.GetObjectProperty<IStringSource>(PropertyName.Event);


	/// <summary>
	/// Get 1st extra data of log.
	/// </summary>
	public IStringSource? Extra1 => this.GetObjectProperty<IStringSource>(PropertyName.Extra1);


	/// <summary>
	/// Get 10th extra data of log.
	/// </summary>
	public IStringSource? Extra10 => this.GetObjectProperty<IStringSource>(PropertyName.Extra10);
	
	
	/// <summary>
	/// Get 11st extra data of log.
	/// </summary>
	public IStringSource? Extra11 => this.GetObjectProperty<IStringSource>(PropertyName.Extra11);
	
	
	/// <summary>
	/// Get 12nd extra data of log.
	/// </summary>
	public IStringSource? Extra12 => this.GetObjectProperty<IStringSource>(PropertyName.Extra12);
	
	
	/// <summary>
	/// Get 13rd extra data of log.
	/// </summary>
	public IStringSource? Extra13 => this.GetObjectProperty<IStringSource>(PropertyName.Extra13);
	
	
	/// <summary>
	/// Get 14th extra data of log.
	/// </summary>
	public IStringSource? Extra14 => this.GetObjectProperty<IStringSource>(PropertyName.Extra14);
	
	
	/// <summary>
	/// Get 15th extra data of log.
	/// </summary>
	public IStringSource? Extra15 => this.GetObjectProperty<IStringSource>(PropertyName.Extra15);
	
	
	/// <summary>
	/// Get 16th extra data of log.
	/// </summary>
	public IStringSource? Extra16 => this.GetObjectProperty<IStringSource>(PropertyName.Extra16);
	
	
	/// <summary>
	/// Get 17th extra data of log.
	/// </summary>
	public IStringSource? Extra17 => this.GetObjectProperty<IStringSource>(PropertyName.Extra17);
	
	
	/// <summary>
	/// Get 18th extra data of log.
	/// </summary>
	public IStringSource? Extra18 => this.GetObjectProperty<IStringSource>(PropertyName.Extra18);
	
	
	/// <summary>
	/// Get 19th extra data of log.
	/// </summary>
	public IStringSource? Extra19 => this.GetObjectProperty<IStringSource>(PropertyName.Extra19);


	/// <summary>
	/// Get 2nd extra data of log.
	/// </summary>
	public IStringSource? Extra2 => this.GetObjectProperty<IStringSource>(PropertyName.Extra2);
	
	
	/// <summary>
	/// Get 20th extra data of log.
	/// </summary>
	public IStringSource? Extra20 => this.GetObjectProperty<IStringSource>(PropertyName.Extra20);


	/// <summary>
	/// Get 3rd extra data of log.
	/// </summary>
	public IStringSource? Extra3 => this.GetObjectProperty<IStringSource>(PropertyName.Extra3);


	/// <summary>
	/// Get 4th extra data of log.
	/// </summary>
	public IStringSource? Extra4 => this.GetObjectProperty<IStringSource>(PropertyName.Extra4);


	/// <summary>
	/// Get 5th extra data of log.
	/// </summary>
	public IStringSource? Extra5 => this.GetObjectProperty<IStringSource>(PropertyName.Extra5);


	/// <summary>
	/// Get 6th extra data of log.
	/// </summary>
	public IStringSource? Extra6 => this.GetObjectProperty<IStringSource>(PropertyName.Extra6);


	/// <summary>
	/// Get 7th extra data of log.
	/// </summary>
	public IStringSource? Extra7 => this.GetObjectProperty<IStringSource>(PropertyName.Extra7);


	/// <summary>
	/// Get 8th extra data of log.
	/// </summary>
	public IStringSource? Extra8 => this.GetObjectProperty<IStringSource>(PropertyName.Extra8);


	/// <summary>
	/// Get 9th extra data of log.
	/// </summary>
	public IStringSource? Extra9 => this.GetObjectProperty<IStringSource>(PropertyName.Extra9);


	/// <summary>
	/// Get name of file which log read from.
	/// </summary>
	public IStringSource? FileName => this.GetObjectProperty<IStringSource>(PropertyName.FileName);
	
	
	// Get specific object property.
	T? GetObjectProperty<T>(PropertyName name) where T : class
	{
		if (this.objectProperties is null)
			return null;
		var data = this.data;
		var objectPropertyCount = (int)data[ObjectPropertyCountDataOffset];
		var index = Array.BinarySearch(data, ObjectPropertyNamesDataOffset, objectPropertyCount, (byte)name);
		if (index < 0)
			return null;
		return this.objectProperties[index - ObjectPropertyNamesDataOffset] as T;
	}


	// Get specific value property.
	T? GetValueProperty<T>(PropertyName name) where T : struct
	{
		var data = this.data;
		var nameValue = (byte)name;
		var objectPropertyCount = (int)data[ObjectPropertyCountDataOffset];
		var valuePropertyCount = (int)data[ValuePropertyCountDataOffset];
		var valuePropertyNamesOffset = ValuePropertyCountDataOffset + 1 + objectPropertyCount;
		var valueOffset = valuePropertyNamesOffset + valuePropertyCount;
		for (var i = 0; i < valuePropertyCount; ++i)
		{
			var propertyName = data[valuePropertyNamesOffset + i];
			if (propertyName > nameValue)
				return null;
			if (propertyName < nameValue)
			{
				valueOffset += propertyTypeMap[propertyName] switch
				{
					PropertyType.DateTime => 8,
					PropertyType.Int32 => 4,
					PropertyType.TimeSpan => 8,
					_ => throw new NotSupportedException(),
				};
				continue;
			}
			return propertyTypeMap[propertyName] switch
			{
				PropertyType.DateTime => DateTime.FromBinary(readInt64Function(data.AsSpan(valueOffset))) is T dateTimeT ? dateTimeT : null,
				PropertyType.Int32 => readInt32Function(data.AsSpan(valueOffset)) is T int32T ? int32T : null,
				PropertyType.TimeSpan => System.TimeSpan.FromTicks(readInt64Function(data.AsSpan(valueOffset))) is T timeSpanT ? timeSpanT : null,
				_ => throw new NotSupportedException(),
			};
		}
		return null;
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
	/// Check whether given log property is exported by <see cref="Log"/> with <see cref="Int32"/> value or not.
	/// </summary>
	/// <param name="propertyName">Name of property.</param>
	/// <returns>True if given log property is exported.</returns>
	public static bool HasInt32Property(string propertyName)
	{
		SetupPropertyMap();
		return int32PropertyNameSet.Contains(propertyName);
	}


	/// <summary>
	/// Check whether given log property is exported by <see cref="Log"/> with <see cref="Int64"/> value or not.
	/// </summary>
	/// <param name="propertyName">Name of property.</param>
	/// <returns>True if given log property is exported.</returns>
	public static bool HasInt64Property(string propertyName) => false;


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
	public LogLevel Level => (LogLevel)this.data[LevelDataOffset];


	/// <summary>
	/// Get line number.
	/// </summary>
	public int? LineNumber => this.GetValueProperty<int>(PropertyName.LineNumber);


	/// <summary>
	/// Get size of memory usage by the instance in bytes.
	/// </summary>
	public long MemorySize => readInt32Function(this.data.AsSpan(MemorySizeDataOffset));


	/// <summary>
	/// Get message.
	/// </summary>
	public IStringSource? Message => this.GetObjectProperty<IStringSource>(PropertyName.Message);


	/// <summary>
	/// Get ID of process which generates log.
	/// </summary>
	public int? ProcessId => this.GetValueProperty<int>(PropertyName.ProcessId);


	/// <summary>
	/// Get name of process which generates log.
	/// </summary>
	public IStringSource? ProcessName => this.GetObjectProperty<IStringSource>(PropertyName.ProcessName);


	/// <summary>
	/// Get list of log properties exported by <see cref="Log"/>.
	/// </summary>
	public static IList<string> PropertyNames => allPropertyNames;


	/// <summary>
	/// Get the timestamp of this log was read.
	/// </summary>
	public DateTime ReadTime => DateTime.FromBinary(readInt64Function(this.data.AsSpan(ReadTimeDataOffset)));


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
						else if (propertyInfo.PropertyType == typeof(int?) || propertyInfo.PropertyType == typeof(int))
							int32PropertyNameSet.Add(propertyName);
					}
					isPropertyMapReady = true;
				}
			}
		}
	}


	/// <summary>
	/// Get name of source which generates log.
	/// </summary>
	public IStringSource? SourceName => this.GetObjectProperty<IStringSource>(PropertyName.SourceName);


	/// <summary>
	/// Get summary of log.
	/// </summary>
	public IStringSource? Summary => this.GetObjectProperty<IStringSource>(PropertyName.Summary);


	/// <summary>
	/// Get tags of log.
	/// </summary>
	public IStringSource? Tags => this.GetObjectProperty<IStringSource>(PropertyName.Tags);


	/// <summary>
	/// Get ID of thread which generates log.
	/// </summary>
	public int? ThreadId => this.GetValueProperty<int>(PropertyName.ThreadId);


	/// <summary>
	/// Get name of thread which generates log.
	/// </summary>
	public IStringSource? ThreadName => this.GetObjectProperty<IStringSource>(PropertyName.ThreadName);


	/// <summary>
	/// Get time span.
	/// </summary>
	public TimeSpan? TimeSpan => this.GetValueProperty<TimeSpan>(PropertyName.TimeSpan);


	/// <summary>
	/// Get timestamp.
	/// </summary>
	public DateTime? Timestamp => this.GetValueProperty<DateTime>(PropertyName.Timestamp);


	/// <summary>
	/// Get title of log.
	/// </summary>
	public IStringSource? Title => this.GetObjectProperty<IStringSource>(PropertyName.Title);


	/// <summary>
	/// Try getting the earliest/latest timestamp from <see cref="BeginningTimestamp"/>, <see cref="EndingTimestamp"/> and <see cref="Timestamp"/>.
	/// </summary>
	/// <param name="earliestTimestamp">The earliest timestamp.</param>
	/// <param name="latestTimestamp">The latest timestamp.</param>
	/// <returns>True if the earliest/latest timestamp are valid.</returns>
	public bool TryGetEarliestAndLatestTimestamp([NotNullWhen(true)] out DateTime? earliestTimestamp, [NotNullWhen(true)] out DateTime? latestTimestamp)
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
		value = default;
		if (!Enum.TryParse<PropertyName>(propertyName, out var name) || name == PropertyName.None)
			return false;
		switch (propertyTypeMap[(byte)name])
		{
			case PropertyType.DateTime:
				if (this.GetValueProperty<DateTime>(name) is T dateTimeT)
				{
					value = dateTimeT;
					return true;
				}
				return false;
			case PropertyType.Int32:
				if (this.GetValueProperty<int>(name) is T int32T)
				{
					value = int32T;
					return true;
				}
				return false;
			case PropertyType.TimeSpan:
				if (this.GetValueProperty<TimeSpan>(name) is T timeSpanT)
				{
					value = timeSpanT;
					return true;
				}
				return false;
			case PropertyType.String:
			{
				var s = this.GetObjectProperty<IStringSource>(name);
				if (s is T stringSourceT)
				{
					value = stringSourceT;
					return true;
				}
				if (typeof(T) == typeof(string) && s is not null)
				{
					value = (T)(object?)s.ToString();
					return true;
				}
				return false;
			}
			default:
				return false;
		}
	}
#pragma warning restore CS8600
#pragma warning restore CS8601


	/// <summary>
	/// Try getting the smallest/largest time span from <see cref="BeginningTimeSpan"/>, <see cref="EndingTimeSpan"/> and <see cref="TimeSpan"/>.
	/// </summary>
	/// <param name="smallestTimeSpan">The smallest time span.</param>
	/// <param name="largestTimeSpan">The largest time span.</param>
	/// <returns>True if the smallest/largest time span are valid.</returns>
	public bool TryGetSmallestAndLargestTimeSpan([NotNullWhen(true)] out TimeSpan? smallestTimeSpan, [NotNullWhen(true)] out TimeSpan? largestTimeSpan)
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
	public IStringSource? UserId => this.GetObjectProperty<IStringSource>(PropertyName.UserId);


	/// <summary>
	/// Get name of user which generates log.
	/// </summary>
	public IStringSource? UserName => this.GetObjectProperty<IStringSource>(PropertyName.UserName);
}