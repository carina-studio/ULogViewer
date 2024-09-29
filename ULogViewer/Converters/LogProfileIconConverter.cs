using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from <see cref="LogProfileIcon"/> or <see cref="LogProfile"/> to <see cref="IImage"/>.
	/// </summary>
	class LogProfileIconConverter : IMultiValueConverter
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
		public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			// check target type
			if (!typeof(IImage).IsAssignableFrom(targetType) && targetType != typeof(object))
				return null;
			
			// select name of icon
			string? iconName;
			var iconColor = LogProfileIconColor.Default;
			if (values.IsEmpty())
				return null;
			if (values.Count == 1)
			{
				if (values[0] is ILogProfileIconSource iconSource)
				{
					if (iconSource is LogProfile profile 
					    && ReferenceEquals(profile, LogProfileManager.Default.EmptyProfile))
					{
						iconName = "Empty";
					}
					else
					{
						iconName = iconSource.Icon.ToString();
						iconColor = iconSource.IconColor;
					}
				}
				else if (values[0] is LogProfileIcon icon)
					iconName = icon.ToString();
				else
					return null;
			}
			else if (values[0] is LogProfileIcon icon && values[1] is LogProfileIconColor color)
			{
				iconName = icon.ToString();
				iconColor = color;
			}
			else
				return null;
			
			// select color of icon if specified
			if (parameter is LogProfileIconColor colorByParam)
				iconColor = colorByParam;
			else if (parameter is string strParam && Enum.TryParse<LogProfileIconColor>(strParam, out var iconColorParam))
				iconColor = iconColorParam;

			// get image
			var modeParam = parameter as string;
			var image = (IImage?)null;
			if (modeParam is not null)
				app.Resources.TryGetResource($"Image/LogProfile.{iconName}.{modeParam}", out image);
			if (image is null)
				app.Resources.TryGetResource($"Image/LogProfile.{iconName}", out image);
			if (image is null)
			{
				if (modeParam is not null)
					app.Resources.TryGetResource($"Image/LogProfile.File.{modeParam}", out image);
				if (image is null)
					app.Resources.TryGetResource($"Image/LogProfile.File", out image);
			}
			if (image is null)
				return null;
			
			// apply color
			if (iconColor != LogProfileIconColor.Default && modeParam is null)
			{
				var geometry = ((image as DrawingImage)?.Drawing as GeometryDrawing)?.Geometry;
				if (geometry != null && app.Styles.TryGetResource<IBrush>($"Brush/LogProfileIconColor.{iconColor}", out var brush))
				{
					image = new DrawingImage(new GeometryDrawing
					{
						Brush = brush,
						Geometry = geometry,
					});
				}
			}
			return image;
		}


		// Convert back.
		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
	}
}
