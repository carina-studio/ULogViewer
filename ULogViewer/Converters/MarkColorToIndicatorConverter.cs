using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters
{
    /// <summary>
    /// Convert from <see cref="MarkColor"/> to <see cref="IBrush"/>.
    /// </summary>
    class MarkColorToIndicatorConverter : IValueConverter
    {
        /// <summary>
        /// Default instance.
        /// </summary>
        public static readonly MarkColorToIndicatorConverter Default = new MarkColorToIndicatorConverter();


        // Static fields.
        static readonly App App = App.Current;


        // Constructor.
        MarkColorToIndicatorConverter()
        { }


        /// <inheritdoc/>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (targetType != typeof(object) && !typeof(IBrush).IsAssignableFrom(targetType))
                return null;
            if (value is not MarkColor color)
                return null;
            var brush = (IBrush?)null;
            if (color != MarkColor.None)
                App.TryFindResource($"Brush/SessionView.LogListBox.MarkIndicator.{color}", out brush);
            return brush;
        }


        /// <inheritdoc/>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
    }
}
