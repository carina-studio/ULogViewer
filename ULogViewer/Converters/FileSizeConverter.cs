using Avalonia.Data.Converters;
using CarinaStudio.IO;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert file size bytes to string.
	/// </summary>
	class FileSizeConverter : IValueConverter
	{
		/// <summary>
		/// Default instance.
		/// </summary>
		public static readonly FileSizeConverter Default = new FileSizeConverter();


		// Convert.
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (targetType != typeof(string))
				return null;
			if (value is long longValue)
				return longValue.ToFileSizeString();
			if (value is int intValue)
				return intValue.ToFileSizeString();
			return null;
		}


		// COnvert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}
}
