using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from enumeration constant to readable string.
	/// </summary>
	class EnumConverter : IValueConverter
	{
		// Fields.
		readonly IApplication app;
		readonly Type type;
		readonly string typeName;


		/// <summary>
		/// Initialize new <see cref="EnumConverter"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="type">Type of enumeration.</param>
		public EnumConverter(IApplication app, Type type)
		{
			if (!type.IsEnum)
				throw new ArgumentException($"Type '{type.FullName}' is not an enumeration.");
			this.app = app;
			this.type = type;
			this.typeName = type.Name;
		}


		// Convert.
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (this.type.IsAssignableFrom(value.GetType()))
				return app.GetString($"{this.typeName}.{value}", value.ToString());
			return null;
		}


		// Convert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}


	/// <summary>
	/// <see cref="IValueConverter"/> to convert from enumeration constant to readable string.
	/// </summary>
	/// <typeparam name="T">Type of enumeration.</typeparam>
	class EnumConverter<T> : EnumConverter where T : Enum
	{
		/// <summary>
		/// Initialize new <see cref="EnumConverter{T}"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public EnumConverter(IApplication app) : base(app, typeof(T))
		{ }
	}
}
