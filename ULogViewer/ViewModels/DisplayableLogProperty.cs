using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Property of <see cref="DisplayableLog"/> to be shown on UI.
	/// </summary>
	class DisplayableLogProperty : BaseDisposable, INotifyPropertyChanged
	{
		// Static fields.
		static IList<string>? displayNames;


		// Fields.
		readonly IApplication app;
		readonly string displayNameId;


		/// <summary>
		/// Initialize new <see cref="DisplayableLogProperty"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="name">Name of property.</param>
		/// <param name="displayName">Name for displaying.</param>
		/// <param name="width">Width of UI field to show property in pixels.</param>
		public DisplayableLogProperty(IApplication app, string name, string? displayName, int? width)
		{
			this.app = app;
			this.displayNameId = displayName ?? name;
			this.DisplayName = LogPropertyNameConverter.Default.Convert(this.displayNameId);
			this.Name = name;
			this.Width = width;
			app.StringsUpdated += this.OnApplicationStringsUpdated;
		}


		/// <summary>
		/// Initialize new <see cref="DisplayableLogProperty"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="logProperty"><see cref="LogProperty"/> defined in <see cref="LogProfile"/>.</param>
		public DisplayableLogProperty(IApplication app, LogProperty logProperty)
		{
			this.app = app;
			this.displayNameId = logProperty.DisplayName;
			this.Name = logProperty.Name switch
			{
				nameof(Log.BeginningTimestamp) => nameof(DisplayableLog.BeginningTimestampString),
				nameof(Log.EndingTimestamp) => nameof(DisplayableLog.EndingTimestampString),
				nameof(Log.Timestamp) => nameof(DisplayableLog.TimestampString),
				_ => logProperty.Name,
			};
			this.DisplayName = LogPropertyNameConverter.Default.Convert(this.displayNameId);
			this.Width = logProperty.Width;
			app.StringsUpdated += this.OnApplicationStringsUpdated;
		}


		/// <summary>
		/// Get name for displaying.
		/// </summary>
		public string DisplayName { get; private set; }


		/// <summary>
		/// Get available display names.
		/// </summary>
		public static IList<string> DisplayNames
		{
			get
			{
				if (displayNames == null)
				{
					displayNames = new List<string>(Log.PropertyNames).Also(it =>
					{
						it.Add("Author");
						it.Add("Child");
						it.Add("Children");
						it.Add("Code");
						it.Add("Commit");
						it.Add("Content");
						it.Add("Count");
						it.Add("Duration");
						it.Add("ID");
						it.Add("Name");
						it.Add("Number");
						it.Add("Parent");
						it.Add("Path");
						it.Add("RawData");
						it.Add("ShortName");
						it.Add("Tag");
						it.Add("Type");
						it.Add("Uri");
						it.Sort();
					}).AsReadOnly();
				}
				return displayNames;
			}
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			this.app.StringsUpdated -= this.OnApplicationStringsUpdated;
		}


		/// <summary>
		/// Name of property.
		/// </summary>
		public string Name { get; }


		// Called when application strings updated.
		void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			var newDisplayName = LogPropertyNameConverter.Default.Convert(this.displayNameId);
			if (this.DisplayName == newDisplayName)
				return;
			this.DisplayName = newDisplayName;
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
		}


		/// <summary>
		/// Raised when property changed.
		/// </summary>
		public event PropertyChangedEventHandler? PropertyChanged;


		/// <summary>
		/// Width of UI field to show property in pixels.
		/// </summary>
		public int? Width { get; }
	}
}
