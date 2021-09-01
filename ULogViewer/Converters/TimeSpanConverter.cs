using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert <see cref="TimeSpan"/> to string.
	/// </summary>
	class TimeSpanConverter : IValueConverter
	{
		/// <summary>
		/// Default instance.
		/// </summary>
		public static readonly TimeSpanConverter Default = new TimeSpanConverter(App.Current);


		// Fields.
		readonly IApplication app;


		// Constructor.
		TimeSpanConverter(IApplication app) => this.app = app;


		// Convert.
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is TimeSpan timeSpan)
			{
				if (timeSpan.Days > 0)
					return this.app.GetFormattedString("TimeSpanConverter.Days", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
				if (timeSpan.Hours > 0)
					return this.app.GetFormattedString("TimeSpanConverter.Hours", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
				if (timeSpan.Minutes > 0)
					return this.app.GetFormattedString("TimeSpanConverter.Minutes", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
				return this.app.GetFormattedString("TimeSpanConverter.Seconds", timeSpan.Seconds, timeSpan.Milliseconds);
			}
			return null;
		}


		// Convert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}
}
