using System;

namespace CarinaStudio.ULogViewer.Logs.Profiles
{
	/// <summary>
	/// Log property to display.
	/// </summary>
	class LogProperty
	{
		/// <summary>
		/// Initialize new <see cref="LogProperty"/> instance.
		/// </summary>
		/// <param name="name">Name of property of log.</param>
		/// <param name="displayName">Name which is suitable to display on UI.</param>
		/// <param name="width">Width of UI field to show property in characters.</param>
		public LogProperty(string name, string? displayName, int? width)
		{
			this.DisplayName = displayName ?? name;
			this.Name = name;
			this.Width = width;
		}


		/// <summary>
		/// Name which is suitable to display on UI.
		/// </summary>
		public string DisplayName { get; }


		// Check equality.
		public override bool Equals(object? obj)
		{
			if (obj is LogProperty logProperty)
			{
				return this.Name == logProperty.Name
					&& this.DisplayName == logProperty.DisplayName
					&& this.Width == logProperty.Width;
			}
			return false;
		}


		// Calculate hash-code.
		public override int GetHashCode() => this.Name?.GetHashCode() ?? 0;


		/// <summary>
		/// Name of property of log.
		/// </summary>
		public string Name { get; }


		// Get readable string.
		public override string ToString() => this.Name;


		/// <summary>
		/// Width of UI field to show property in characters.
		/// </summary>
		public int? Width { get; }
	}
}
