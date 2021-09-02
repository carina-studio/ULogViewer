using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Builder to build <see cref="Log"/> instance.
	/// </summary>
	class LogBuilder
	{
		// Fields.
		int maxExtraNumber = 0;
		readonly Dictionary<string, string> properties = new Dictionary<string, string>();


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
			if (propertyName.StartsWith("Extra")
				&& int.TryParse(propertyName.Substring(5), out var number)
				&& number > this.maxExtraNumber)
			{
				this.maxExtraNumber = number;
			}
			if (this.properties.TryGetValue(propertyName, out var str))
				properties[propertyName] = str + "\n" + value;
			else
				properties[propertyName] = value;
		}


		/// <summary>
		/// Build new <see cref="Log"/> instance.
		/// </summary>
		/// <returns><see cref="Log"/>.</returns>
		public Log Build() => new Log(this);


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
				return CompressedString.Create(str, this.StringCompressionLevel);
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
			if (this.properties.TryGetValue(propertyName, out var str) && int.TryParse(str, out var value))
				return value;
			return null;
		}


		/// <summary>
		/// Get log property as <see cref="long"/> or return null if unable to get the property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <returns>Value or null.</returns>
		public long? GetInt64OrNull(string propertyName)
		{
			if (this.properties.TryGetValue(propertyName, out var str) && long.TryParse(str, out var value))
				return value;
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
		/// Get maximum extra number had been set to this builder.
		/// </summary>
		public int MaxExtraNumber { get => this.maxExtraNumber; }


		/// <summary>
		/// Get <see cref="LogReader"/> which owns this builder.
		/// </summary>
		public LogReader? Reader { get; }


		/// <summary>
		/// Clear all log properties.
		/// </summary>
		public void Reset()
		{
			this.maxExtraNumber = 0;
			this.properties.Clear();
		}


		/// <summary>
		/// Set or override value to property.
		/// </summary>
		/// <param name="propertyName">Name of property of log.</param>
		/// <param name="value">Value to set.</param>
		public void Set(string propertyName, string value)
		{
			if(propertyName.StartsWith("Extra") 
				&& int.TryParse(propertyName.Substring(5), out var number)
				&& number > this.maxExtraNumber)
			{
				this.maxExtraNumber = number;
			}
			properties[propertyName] = value;
		}


		/// <summary>
		/// Get or set string compression level.
		/// </summary>
		public CompressedString.Level StringCompressionLevel { get; set; } = CompressedString.Level.None;
	}
}
