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
    class MarkColorToBackgroundConverter : IValueConverter
    {
        /// <summary>
        /// Default instance.
        /// </summary>
        public static readonly MarkColorToBackgroundConverter Default = new MarkColorToBackgroundConverter(true);
        /// <summary>
        /// Default instance without color for <see cref="MarkColor.Default"/>.
        /// </summary>
        public static readonly MarkColorToBackgroundConverter DefaultWithoutDefaultColor = new MarkColorToBackgroundConverter(false);


        // Static fields.
        static readonly App App = App.Current;


        // Fields.
        readonly bool hasDefaultColor;


        // Constructor.
        MarkColorToBackgroundConverter(bool hasDefaultColor) => this.hasDefaultColor = hasDefaultColor;


        /// <inheritdoc/>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (targetType != typeof(object) && !typeof(IBrush).IsAssignableFrom(targetType))
                return null;
            if (value is not MarkColor color)
                return null;
            var brush = (IBrush?)null;
            switch (color)
            {
                case MarkColor.None:
                    break;
                case MarkColor.Default:
                    if (this.hasDefaultColor)
                        App.TryFindResource($"Brush/SessionView.LogListBox.Item.Background.Marked.Default", out brush);
                    break;
                default:
                    App.TryFindResource($"Brush/SessionView.LogListBox.Item.Background.Marked.{color}", out brush);
                    break;
            }
            return brush;
        }


        /// <inheritdoc/>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
    }
}
