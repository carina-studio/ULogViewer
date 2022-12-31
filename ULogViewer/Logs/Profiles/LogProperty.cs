using System;

namespace CarinaStudio.ULogViewer.Logs.Profiles
{
	/// <summary>
	/// Log property to display.
	/// </summary>
	class LogProperty : IEquatable<LogProperty>
	{
		/// <summary>
		/// Initialize new <see cref="LogProperty"/> instance.
		/// </summary>
		/// <param name="name">Name of property of log.</param>
		/// <param name="displayName">Name which is suitable to display on UI.</param>
		/// <param name="foregroundColor">Foreground color.</param>
		/// <param name="width">Width of UI field to show property in pixels.</param>
		public LogProperty(string name, string? displayName, LogPropertyForegroundColor foregroundColor, int? width)
		{
			this.DisplayName = displayName ?? name;
			this.ForegroundColor = foregroundColor;
			this.Name = name;
			this.Width = width;
		}


		/// <summary>
		/// Name which is suitable to display on UI.
		/// </summary>
		public string DisplayName { get; }

		
		/// <inheritdoc/>
		public bool Equals(LogProperty? property) =>
			property is not null
			&& this.Name == property.Name
			&& this.DisplayName == property.DisplayName
			&& this.ForegroundColor == property.ForegroundColor
			&& this.Width == property.Width; 


		/// <inheritdoc/>
		public override bool Equals(object? obj) =>
			obj is LogProperty property && this.Equals(property);
		

		/// <summary>
		/// Get foreground color.
		/// </summary>
		public LogPropertyForegroundColor ForegroundColor { get; }


		/// <inheritdoc/>
		public override int GetHashCode() => this.Name?.GetHashCode() ?? 0;


		/// <summary>
		/// Name of property of log.
		/// </summary>
		public string Name { get; }


		/// <summary>
		/// Equality operator.
		/// </summary>
		public static bool operator ==(LogProperty? x, LogProperty? y) => x?.Equals(y) ?? y is null;


		/// <summary>
		/// Inquality operator.
		/// </summary>
		public static bool operator !=(LogProperty? x, LogProperty? y) => !(x?.Equals(y) ?? y is null);


		// Get readable string.
		public override string ToString() => this.Name;


		/// <summary>
		/// Width of UI field to show property in pixels.
		/// </summary>
		public int? Width { get; }
	}


	/// <summary>
	/// Foreground color of log property.
	/// </summary>
	enum LogPropertyForegroundColor
	{
		/// <summary>
		/// Not specified.
		/// </summary>
		None,
		/// <summary>
		/// According to level of log.
		/// </summary>
		Level,
	}
}
