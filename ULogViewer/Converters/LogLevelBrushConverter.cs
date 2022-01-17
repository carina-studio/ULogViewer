using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from <see cref="LogLevel"/> to <see cref="IBrush"/>.
	/// </summary>
	class LogLevelBrushConverter : IValueConverter
	{
		/// <summary>
		/// Default instance.
		/// </summary>
		public static readonly LogLevelBrushConverter Default = new LogLevelBrushConverter(App.Current);


		// Fields.
		readonly App app;


		// Constructor.
		LogLevelBrushConverter(App app)
		{
			this.app = app;
		}


		// Convert.
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is LogLevel level && targetType == typeof(IBrush))
			{
				var state = parameter as string;
				var brush = (object?)null;
				if (string.IsNullOrEmpty(state))
					this.app.Styles.TryGetResource($"Brush/LogLevel.{level}", out brush);
				else
					this.app.Styles.TryGetResource($"Brush/LogLevel.{level}.{state}", out brush);
				return brush as IBrush;
			}
			return null;
		}


		// Convert back.
		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
	}
}
