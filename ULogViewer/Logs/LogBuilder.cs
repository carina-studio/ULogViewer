using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using CarinaStudio.ULogViewer.Text;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Builder to build <see cref="Log"/> instance.
	/// </summary>
	class LogBuilder
	{
		// Static fields.
		static readonly IDictionary<ushort, IStringSource> SharedStringSources = new ConcurrentDictionary<ushort, IStringSource>();


		// Fields.
		Func<ReadOnlyMemory<char>, IStringSource> getStringImpl = GetStringWithBalanceMup;
		MemoryUsagePolicy memoryUsagePolicy = MemoryUsagePolicy.Balance;
		readonly Dictionary<string, object> properties = new();


		/// <summary>
		/// Initialize new <see cref="LogBuilder"/> instance.
		/// </summary>
		public LogBuilder()
		{ }
		
		
		/// <summary>
		/// Append value into property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to append.</param>
		public void Append(string propertyName, string value)
		{
			if (this.properties.TryGetValue(propertyName, out var current))
			{
				if (current is ReadOnlyMemory<char> memory)
				{
					var oldLength = memory.Length;
					var newValue = new char[oldLength + value.Length];
					memory.CopyTo(newValue);
					value.AsMemory().CopyTo(new Memory<char>(newValue).Slice(oldLength));
					properties[propertyName] = newValue;
					return;
				}
				if (current is string str)
				{
					properties[propertyName] = str + value;
					return;
				}
			}
			properties[propertyName] = value;
		}


		/// <summary>
		/// Append value into property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to append.</param>
		public void Append(string propertyName, ReadOnlyMemory<char> value)
		{
			if (this.properties.TryGetValue(propertyName, out var current))
			{
				if (current is ReadOnlyMemory<char> memory)
				{
					var oldLength = memory.Length;
					var newValue = new char[oldLength + value.Length];
					memory.CopyTo(newValue);
					value.CopyTo(new Memory<char>(newValue).Slice(oldLength));
					properties[propertyName] = newValue;
					return;
				}
				if (current is string str)
				{
					properties[propertyName] = str + new string(value.Span);
					return;
				}
			}
			properties[propertyName] = value;
		}
		
		
		/// <summary>
		/// Append value to next line of property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to append.</param>
		public void AppendToNextLine(string propertyName, string value)
		{
			if (this.properties.TryGetValue(propertyName, out var current))
			{
				if (current is ReadOnlyMemory<char> memory)
				{
					var oldLength = memory.Length;
					var newValue = new char[oldLength + value.Length + 1];
					memory.CopyTo(newValue);
					newValue[oldLength] = '\n';
					value.AsMemory().CopyTo(new Memory<char>(newValue).Slice(oldLength + 1));
					properties[propertyName] = newValue;
					return;
				}
				if (current is string str)
				{
					properties[propertyName] = str + '\n' + value;
					return;
				}
			}
			properties[propertyName] = value;
		}


		/// <summary>
		/// Append value to next line of property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to append.</param>
		public void AppendToNextLine(string propertyName, ReadOnlyMemory<char> value)
		{
			if (this.properties.TryGetValue(propertyName, out var current))
			{
				if (current is ReadOnlyMemory<char> memory)
				{
					var oldLength = memory.Length;
					var newValue = new char[oldLength + value.Length + 1];
					memory.CopyTo(newValue);
					newValue[oldLength] = '\n';
					value.CopyTo(new Memory<char>(newValue).Slice(oldLength + 1));
					properties[propertyName] = newValue;
					return;
				}
				if (current is string str)
				{
					properties[propertyName] = str + '\n' + new string(value.Span);
					return;
				}
			}
			properties[propertyName] = value;
		}


		/// <summary>
		/// Build new <see cref="Log"/> instance.
		/// </summary>
		/// <returns><see cref="Log"/>.</returns>
		public Log Build() => new(this);


		/// <summary>
		/// Build new <see cref="Log"/> instance and reset all log properties.
		/// </summary>
		/// <returns><see cref="Log"/>.</returns>
		public Log BuildAndReset() => new Log(this).Also(_ => this.Reset());


		/// <summary>
		/// Get log property as <see cref="DateTime"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public DateTime? GetDateTimeOrNull(string propertyName)
		{
			if (!this.properties.TryGetValue(propertyName, out var value))
				return null;
			var span = value switch
			{
				ReadOnlyMemory<char> memory => memory.Span,
				string s => s.AsSpan(),
				_ => default,
			};
			if (DateTime.TryParse(span, out var dateTimeValue))
				return dateTimeValue;
			if (long.TryParse(span, out var longValue))
				return DateTime.FromBinary(longValue);
			return null;
		}


		/// <summary>
		/// Get log property as enumeration or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public T? GetEnumOrNull<T>(string propertyName) where T : struct, Enum
		{
			if (!this.properties.TryGetValue(propertyName, out var value))
				return null;
			var span = value switch
			{
				ReadOnlyMemory<char> memory => memory.Span,
				string s => s.AsSpan(),
				_ => default,
			};
			if (Enum.TryParse<T>(span, out var enumValue))
				return enumValue;
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="int"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public int? GetInt32OrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var value))
			{
				int intValue;
				var span = value switch
				{
					ReadOnlyMemory<char> memory => memory.Span,
					string s => s.AsSpan(),
					_ => default,
				};
				if (span.Length > 2 && span[0] == '0' && span[1] == 'x')
				{
					if (int.TryParse(span[2..], NumberStyles.AllowHexSpecifier, null, out intValue))
						return intValue;
				}
				else if (int.TryParse(span, out intValue))
					return intValue;
			}
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="long"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public long? GetInt64OrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var value))
			{
				long longValue;
				var span = value switch
				{
					ReadOnlyMemory<char> memory => memory.Span,
					string s => s.AsSpan(),
					_ => default,
				};
				if (span.Length > 2 && span[0] == '0' && span[1] == 'x')
				{
					span = span[^1] == 'L' ? span[2..^1] : span[2..];
					if (long.TryParse(span, NumberStyles.AllowHexSpecifier, null, out longValue))
						return longValue;
				}
				else if (long.TryParse(span, out longValue))
					return longValue;
			}
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="string"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public IStringSource? GetStringOrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var value))
			{
				if (value is IStringSource stringSource)
					return stringSource;
				if (value is ReadOnlyMemory<char> memory)
				{
					if (memory.Length == 0)
						return IStringSource.Empty;
					var stringSpan = memory.Span;
					if (memory.Length <= 2 && stringSpan[0] <= 127 && (memory.Length == 1 || stringSpan[1] <= 127))
					{
						var key = (ushort)((stringSpan[0] << 8) | (memory.Length == 1 ? 0 : stringSpan[1]));
						if (SharedStringSources.TryGetValue(key, out var sharedStringSource))
							return sharedStringSource;
						sharedStringSource = new SmallStringSource(stringSpan);
						SharedStringSources.TryAdd(key, sharedStringSource);
						return sharedStringSource;
					}
					return this.getStringImpl(memory);
				}
				if (value is string s)
				{
					if (s.Length == 0)
						return IStringSource.Empty;
					var stringSpan = s.AsSpan();
					if (s.Length <= 2 && stringSpan[0] <= 127 && (s.Length == 1 || stringSpan[1] <= 127))
					{
						var key = (ushort)((stringSpan[0] << 8) | (s.Length == 1 ? 0 : stringSpan[1]));
						if (SharedStringSources.TryGetValue(key, out var sharedStringSource))
							return sharedStringSource;
						sharedStringSource = new SmallStringSource(stringSpan);
						SharedStringSources.TryAdd(key, sharedStringSource);
						return sharedStringSource;
					}
					return this.getStringImpl(s.AsMemory());
				}
			}
			return null;
		}
		
		
		// Get string for Balance memory usage policy.
		static IStringSource GetStringWithBalanceMup(ReadOnlyMemory<char> s)
		{
			var length = s.Length;
			if (length == 0)
				return IStringSource.Empty;
			if (length <= SmallStringSource.MaxLength)
				return new SmallStringSource(s);
			return length <= 64 || length > 256
				? new CompressedStringSource(s)
				: new Utf8StringSource(s);
		}
		
		
		// Get string for BetterPerformance memory usage policy.
		static IStringSource GetStringWithBetterPerformanceMup(ReadOnlyMemory<char> s)
		{
			var length = s.Length;
			if (length == 0)
				return IStringSource.Empty;
			if (length <= SmallStringSource.MaxLength)
				return new SmallStringSource(s);
			return length <= 64 || length > 256
				? new Utf8StringSource(s)
				: new SimpleStringSource(s);
		}
		
		
		// Get string for LessMemoryUsage memory usage policy.
		static IStringSource GetStringWithLessMemoryUsageMup(ReadOnlyMemory<char> s)
		{
			var length = s.Length;
			if (length == 0)
				return IStringSource.Empty;
			if (length <= SmallStringSource.MaxLength)
				return new SmallStringSource(s);
			return new CompressedStringSource(s);
		}


		/// <summary>
		/// Get log property as <see cref="TimeSpan"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public TimeSpan? GetTimeSpanOrNull(string propertyName)
		{
			if (!this.properties.TryGetValue(propertyName, out var value))
				return null;
			var span = value switch
			{
				ReadOnlyMemory<char> memory => memory.Span,
				string s => s.AsSpan(),
				_ => default,
			};
			if (double.TryParse(span, out var ms))
				return TimeSpan.FromMilliseconds(ms);
			if (TimeSpan.TryParse(span, out var timeSpan))
				return timeSpan;
			return null;
		}


		/// <summary>
		/// Check whether no log property has been set or not.
		/// </summary>
		/// <returns></returns>
		public bool IsEmpty() => this.properties.Count == 0;


		/// <summary>
		/// Check whether at least one log property has been set or not.
		/// </summary>
		/// <returns></returns>
		public bool IsNotEmpty() => this.properties.Count > 0;


		/// <summary>
		/// Get or set memory usage policy.
		/// </summary>
		public MemoryUsagePolicy MemoryUsagePolicy
		{
			get => this.memoryUsagePolicy;
			set
			{
				if (this.memoryUsagePolicy == value)
					return;
				this.memoryUsagePolicy = value;
				this.getStringImpl = value switch
				{
					MemoryUsagePolicy.BetterPerformance => GetStringWithBetterPerformanceMup,
					MemoryUsagePolicy.LessMemoryUsage => GetStringWithLessMemoryUsageMup,
					_ => GetStringWithBalanceMup,
				};
			}
		}


		/// <summary>
		/// Get number of properties has been set to builder.
		/// </summary>
		public int PropertyCount => this.properties.Count;


		/// <summary>
		/// Get all property names in the builder.
		/// </summary>
		public ICollection<string> PropertyNames => this.properties.Keys;


		/// <summary>
		/// Clear all log properties.
		/// </summary>
		public void Reset()
		{
			this.properties.Clear();
		}


		/// <summary>
		/// Set or override value to property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to set.</param>
		public void Set(string propertyName, IStringSource value) =>
			properties[propertyName] = value;
		
		
		/// <summary>
		/// Set or override value to property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to set.</param>
		public void Set(string propertyName, string value) =>
			properties[propertyName] = value;


		/// <summary>
		/// Set or override value to property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to set.</param>
		public void Set(string propertyName, ReadOnlyMemory<char> value) =>
			properties[propertyName] = value;
	}
}
