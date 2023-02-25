using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Net;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// Converters to convert from <see cref="SearchProvider"/> to <see cref="IImage"/>.
/// </summary>
static class SearchProviderIconConverters
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly IValueConverter Default = new FuncValueConverter<SearchProvider, IImage?>(provider =>
        provider != null ? App.CurrentOrNull?.FindResourceOrDefault<IImage>($"Image/SearchProvider.{provider.Id}.Colored") : null);
}