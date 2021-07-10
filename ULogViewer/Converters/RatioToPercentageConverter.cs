using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Text;

namespace CarinaStudio.ULogViewer.Converters
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert ratio to percentage.
	/// </summary>
	class RatioToPercentageConverter : IValueConverter
	{
		// Fields.
		readonly int decimalPlaces;
		readonly string? stringFormat;


		/// <summary>
		/// Initialize new <see cref="RatioToPercentageConverter"/> instance.
		/// </summary>
		/// <param name="decimalPlaces">Decimal places.</param>
		public RatioToPercentageConverter(int decimalPlaces)
		{
			if (decimalPlaces < 0)
				throw new ArgumentOutOfRangeException();
			this.decimalPlaces = decimalPlaces;
			if (decimalPlaces > 0)
			{
				this.stringFormat = new StringBuilder("{0:0.").Also(it =>
				{
					for (var i = decimalPlaces; i > 0; --i)
						it.Append('0');
					it.Append("}%");
				}).ToString();
			}
		}


		// Convert.
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var ratio = value.Let(it =>
			{
				if (it is IConvertible convertible)
				{
					try
					{
						return convertible.ToDouble(null);
					}
					catch
					{ }
				}
				return double.NaN;
			});
			if (double.IsNaN(ratio))
				return null;
			return targetType.Let(_ =>
			{
				if (targetType == typeof(string))
				{
					if (this.stringFormat != null)
						return (object)string.Format(this.stringFormat, ratio * 100);
					return $"{(int)(ratio * 100 + 0.5)}%";
				}
				if (targetType == typeof(double))
					return ratio * 100;
				if (targetType == typeof(float))
					return (float)(ratio * 100);
				if (targetType == typeof(int))
					return (int)(ratio * 100 + 0.5);
				return null;
			});
		}


		// Convert back.
		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
	}
}
