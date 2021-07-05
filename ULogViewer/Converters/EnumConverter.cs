using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from enumeration constant to readable string.
	/// </summary>
	/// <typeparam name="T">Type of enumeration.</typeparam>
	class EnumConverter<T> : IValueConverter where T : Enum
	{
		// Fields.
		readonly IApplication app;


		/// <summary>
		/// Initialize new <see cref="EnumConverter{T}"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public EnumConverter(IApplication app)
		{
			this.app = app;
		}


		// Convert.
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is T enumValue)
				return app.GetString($"{typeof(T).Name}.{enumValue}", enumValue.ToString());
			return null;
		}


		// Convert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}
}
