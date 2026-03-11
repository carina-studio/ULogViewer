using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.Data.Converters;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// Converter to convert from <see cref="ILogDataSourceProvider"/> to <see cref="IImage"/>.
/// </summary>
class LogDataSourceProviderIconConverter : BaseValueConverter<ILogDataSourceProvider?, IImage?>
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly IValueConverter Default = new LogDataSourceProviderIconConverter();


    // Constructor.
    LogDataSourceProviderIconConverter()
    { }


    // Convert.
    protected override IImage? Convert(ILogDataSourceProvider? value, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return null;
        var resName = value is not ScriptLogDataSourceProvider
            ? (parameter as string) == "Light"
                ? $"Image/LogDataSourceProvider.{value.Name}.Light"
                : $"Image/LogDataSourceProvider.{value.Name}"
            : (parameter as string) == "Light"
                ? "Image/Code.Outline.Light"
                : "Image/Code.Outline";
        if (App.Current.TryFindResource<IImage>(resName, out var icon))
            return icon;
        resName = (parameter as string) == "Light"
            ? "Image/LogDataSourceProvider.Light"
            : "Image/LogDataSourceProvider";
        App.Current.TryFindResource(resName, out icon);
        return icon;
    }
}