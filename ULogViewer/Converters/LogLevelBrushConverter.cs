using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.Controls;
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
		/// Default instance for background.
		/// </summary>
		public static readonly LogLevelBrushConverter Background = new(App.Current, "Background");
		/// <summary>
		/// Default instance for foreground.
		/// </summary>
		public static readonly LogLevelBrushConverter Foreground = new(App.Current, "Foreground");


		// Fields.
		readonly App app;
		readonly string prefix;


		// Constructor.
		LogLevelBrushConverter(App app, string prefix)
		{
			this.app = app;
			this.prefix = prefix;
		}


		// Convert.
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is LogLevel level && targetType == typeof(IBrush))
			{
				var state = parameter as string;
				if (string.IsNullOrEmpty(state))
					return this.app.FindResourceOrDefault<IBrush>($"Brush/ULogViewer.LogLevel.{level}.{this.prefix}");
				return this.app.FindResourceOrDefault<IBrush>($"Brush/ULogViewer.LogLevel.{level}.{this.prefix}.{state}");
			}
			return null;
		}


		// Convert back.
		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
	}
}
