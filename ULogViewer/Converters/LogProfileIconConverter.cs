using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from <see cref="LogProfileIcon"/> to <see cref="IBitmap"/>.
	/// </summary>
	class LogProfileIconConverter : IValueConverter
	{
		// Fields.
		readonly IAssetLoader assetLoader = AvaloniaLocator.Current.GetService<IAssetLoader>();
		readonly Dictionary<LogProfileIcon, IBitmap> icons = new Dictionary<LogProfileIcon, IBitmap>();
		readonly IconSize iconSize;


		/// <summary>
		/// Initialize new <see cref="LogProfileIconConverter"/> instance.
		/// </summary>
		/// <param name="iconSize">Desired icon size.</param>
		public LogProfileIconConverter(IconSize iconSize)
		{
			this.iconSize = iconSize;
		}


		// Convert.
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not LogProfileIcon icon)
				return null;
			if (this.icons.TryGetValue(icon, out var bitmap))
				return bitmap;
			var bitmapSize = (this.iconSize == IconSize.Large ? 128 : 64);
			using var stream = this.assetLoader.Open(new Uri($"avares://ULogViewer/Resources/LogProfile.{icon}.{bitmapSize}px.png"));
			bitmap = new Bitmap(stream);
			this.icons[icon] = bitmap;
			return bitmap;
		}


		// Convert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}
}
