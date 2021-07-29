using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from <see cref="LogProfileIcon"/> to <see cref="Drawing"/>.
	/// </summary>
	class LogProfileIconConverter : IValueConverter
	{
		/// <summary>
		/// Default instance.
		/// </summary>
		public static readonly LogProfileIconConverter Default = new LogProfileIconConverter(App.Current);


		// Fields.
		readonly App app;


		// Constructor.
		LogProfileIconConverter(App app)
		{
			this.app = app;
		}


		// Convert.
		public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is not LogProfileIcon icon)
				return null;
			if (!typeof(Drawing).IsAssignableFrom(targetType) && targetType != typeof(object))
				return null;
			var mode = parameter as string;
			var res = (object?)null;
			if (mode != null)
			{
				if (app.Resources.TryGetResource($"Drawing.LogProfile.{icon}.{mode}", out res) && res is Drawing)
					return res;
			}
			if (app.Resources.TryGetResource($"Drawing.LogProfile.{icon}", out res) && res is Drawing)
				return res;
			if (mode != null)
			{
				if (app.Resources.TryGetResource($"Drawing.LogProfile.File.{mode}", out res) && res is Drawing)
					return res;
			}
			if (app.Resources.TryGetResource($"Drawing.LogProfile.File", out res) && res is Drawing)
				return res;
			return null;
		}


		// Convert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}
}
