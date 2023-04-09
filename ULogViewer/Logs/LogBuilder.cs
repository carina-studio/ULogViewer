using System;
using System.Collections.Generic;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Builder to build <see cref="Log"/> instance.
	/// </summary>
	class LogBuilder
	{
		// Fields.
		readonly Dictionary<string, string> properties = new();


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
			if (this.properties.TryGetValue(propertyName, out var str))
				properties[propertyName] = str + value;
			else
				properties[propertyName] = value;
		}


		/// <summary>
		/// Append value to next line of property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to append.</param>
		public void AppendToNextLine(string propertyName, string value)
		{
			if (this.properties.TryGetValue(propertyName, out var str))
				properties[propertyName] = str + "\n" + value;
			else
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
		/// Get log property as <see cref="CompressedString"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public CompressedString? GetCompressedStringOrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var str))
			{
				var length = str.Length;
				if (length == 0)
					return CompressedString.Empty;
				var compressedString = this.MemoryUsagePolicy switch
				{
					MemoryUsagePolicy.BetterPerformance => length <= 64
						? CompressedString.Create(str, CompressedString.Level.Optimal)
						: CompressedString.Create(str, CompressedString.Level.Fast),
					MemoryUsagePolicy.LessMemoryUsage => 
						CompressedString.Create(str, CompressedString.Level.Optimal),
					_ => length <= 64
						? CompressedString.Create(str, CompressedString.Level.Fast)
						: CompressedString.Create(str, CompressedString.Level.Optimal),
				};
				return compressedString;
			}
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="DateTime"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public DateTime? GetDateTimeOrNull(string propertyName)
		{
			if (!this.properties.TryGetValue(propertyName, out var str))
				return null;
			if (DateTime.TryParse(str, out var value))
				return value;
			if (long.TryParse(str, out var longValue))
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
			if (this.properties.TryGetValue(propertyName, out var str) && Enum.TryParse<T>(str, out var value))
				return value;
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="int"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public int? GetInt32OrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var str))
			{
				int value;
				if (str.StartsWith("0x"))
				{
					if (int.TryParse(str[2..^0], NumberStyles.AllowHexSpecifier, null, out value))
						return value;
				}
				else if (int.TryParse(str, out value))
					return value;
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
			if (this.properties.TryGetValue(propertyName, out var str))
			{
				long value;
				if (str.StartsWith("0x"))
				{
					str = str.EndsWith("L") ? str[2..^1] : str[2..^0];
					if (long.TryParse(str, NumberStyles.AllowHexSpecifier, null, out value))
						return value;
				}
				else if (long.TryParse(str, out value))
					return value;
			}
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="string"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public string? GetStringOrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var str))
				return str;
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="TimeSpan"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public TimeSpan? GetTimeSpanOrNull(string propertyName)
		{
			if (!this.properties.TryGetValue(propertyName, out var str))
				return null;
			if (double.TryParse(str, out var ms))
				return TimeSpan.FromMilliseconds(ms);
			if (TimeSpan.TryParse(str, out var value))
				return value;
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
		public MemoryUsagePolicy MemoryUsagePolicy { get; set; } = MemoryUsagePolicy.Balance;


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
		public void Set(string propertyName, string value)
		{
			properties[propertyName] = value;
		}
	}
}
