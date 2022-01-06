using Avalonia.Data.Converters;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert <see cref="LogProperty.Name"/>.
	/// </summary>
	class LogPropertyNameConverter : IValueConverter
	{
		/// <summary>
		/// Default instance.
		/// </summary>
		public static readonly LogPropertyNameConverter Default = new LogPropertyNameConverter(App.Current);


		// Fields.
		readonly App app;


		// Constructor.
		LogPropertyNameConverter(App app)
		{
			this.app = app;
		}


		/// <summary>
		/// Convert from name of <see cref="LogProperty"/> to readable name.
		/// </summary>
		/// <param name="propertyName">Property name.</param>
		/// <returns>Readable name.</returns>
		public string Convert(string propertyName) => this.Convert(propertyName, typeof(string), null, this.app.CultureInfo) as string ?? propertyName;


		// Convert.
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (targetType != typeof(object) && targetType != typeof(string))
				return null;
			if (value is not string name)
				return null;
			return this.app.GetStringNonNull($"LogProperty.{name}", name);
		}


		// Convert back.
		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
	}
}
