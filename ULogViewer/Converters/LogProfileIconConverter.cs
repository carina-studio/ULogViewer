using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from <see cref="LogProfileIcon"/> or <see cref="LogProfile"/> to <see cref="IImage"/>.
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
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			// check target type
			if (!typeof(IImage).IsAssignableFrom(targetType) && targetType != typeof(object))
				return null;

			// select name of icon
			var iconName = (string?)null;
			if (value is LogProfile profile)
			{
				if (profile == LogProfileManager.Default.EmptyProfile)
					iconName = "Empty";
				else
					iconName = profile.Icon.ToString();
			}
			else if (value is LogProfileIcon icon)
				iconName = icon.ToString();
			else
				return null;

			// convert to image
			var mode = parameter as string;
			var res = (object?)null;
			if (mode != null)
			{
				if (app.Resources.TryGetResource($"Image/LogProfile.{iconName}.{mode}", out res) && res is IImage)
					return res;
			}
			if (app.Resources.TryGetResource($"Image/LogProfile.{iconName}", out res) && res is IImage)
				return res;
			if (mode != null)
			{
				if (app.Resources.TryGetResource($"Image/LogProfile.File.{mode}", out res) && res is IImage)
					return res;
			}
			if (app.Resources.TryGetResource($"Image/LogProfile.File", out res) && res is IImage)
				return res;
			return null;
		}


		// Convert back.
		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
	}
}
