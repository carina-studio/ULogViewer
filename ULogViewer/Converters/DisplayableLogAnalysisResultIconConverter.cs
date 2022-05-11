using Avalonia.Media;
using CarinaStudio.AppSuite;
using CarinaStudio.Controls;
using CarinaStudio.Data.Converters;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// Convert from object to icon of <see cref="DisplayableLogAnalysisResult"/>.
/// </summary>
class DisplayableLogAnalysisResultIconConverter : BaseValueConverter<object?, IImage?>
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly DisplayableLogAnalysisResultIconConverter Default = new(App.Current);


    // Fields.
    readonly IAppSuiteApplication app;


    // Construtor.
    DisplayableLogAnalysisResultIconConverter(IAppSuiteApplication app) =>
        this.app = app;


    /// <inheritdoc/>
    protected override IImage? Convert(object? value, object? parameter, CultureInfo culture)
    {
        var type = value switch
        {
            DisplayableLogAnalysisResult result => result.Type,
            DisplayableLogAnalysisResultType => (DisplayableLogAnalysisResultType?)value,
            _ => null,
        };
        if (type.HasValue)
        {
            var image = (IImage?)null;
            switch (type.Value)
            {
                case DisplayableLogAnalysisResultType.Checkpoint:
                case DisplayableLogAnalysisResultType.OperationEnd:
                case DisplayableLogAnalysisResultType.OperationStart:
                case DisplayableLogAnalysisResultType.Performance:
                    app.TryGetResource<IImage>($"Image/{type.Value}.Outline.Colored", out image);
                    return image;
                case DisplayableLogAnalysisResultType.TimeSpan:
                    app.TryGetResource<IImage>($"Image/Clock.Outline.Colored", out image);
                    return image;
                default:
                    app.TryGetResource<IImage>($"Image/Icon.{type.Value}.Outline.Colored", out image);
                    return image;
            }
        }
        return null;
    }
}