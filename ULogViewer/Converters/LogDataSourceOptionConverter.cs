using Avalonia.Controls;
using Avalonia.Data.Converters;
using CarinaStudio.ULogViewer.Logs.DataSources;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// <see cref="IValueConverter"/> to convert from option of <see cref="LogDataSourceOptions"/> to readable string.
/// </summary>
class LogDataSourceOptionConverter : FuncValueConverter<string?, string?>
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly IValueConverter Default = new LogDataSourceOptionConverter();


    // Constructor.
    LogDataSourceOptionConverter() : base(option => (App.CurrentOrNull?.FindResource($"String/LogDataSourceOptions.{option}") as string) ?? option)
    { }
}