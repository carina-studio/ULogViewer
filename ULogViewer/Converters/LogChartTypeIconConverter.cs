using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.AppSuite;
using CarinaStudio.Data.Converters;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System.Globalization;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// <see cref="IValueConverter"/> to convert from <see cref="LogChartType"/> to <see cref="IImage"/>.
/// </summary>
class LogChartTypeIconConverter : BaseValueConverter<LogChartType, IImage?>
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly IValueConverter Default = new LogChartTypeIconConverter(false);
    /// <summary>
    /// Default instance which uses outlined icons.
    /// </summary>
    public static readonly IValueConverter Outline = new LogChartTypeIconConverter(true);
    
    
    // Fields.
    readonly IAppSuiteApplication? app;
    readonly bool outline;
    
    
    // Constructor.
    LogChartTypeIconConverter(bool outline)
    {
        this.app = IAppSuiteApplication.CurrentOrNull;
        this.outline = outline;
    }


    /// <inheritdoc/>
    protected override IImage? Convert(LogChartType value, object? parameter, CultureInfo culture)
    {
        if (this.app is null)
            return null;
        var state = parameter as string;
        var key = value switch
        {
            LogChartType.ValueAreas
                or LogChartType.ValueAreasWithDataPoints => "Image/Chart.Areas"
                                                            + (string.IsNullOrWhiteSpace(state) ? "" : $".{state}"),
            LogChartType.ValueStackedAreas
                or LogChartType.ValueStackedAreasWithDataPoints => "Image/Chart.StackedAreas"
                                                                   + (string.IsNullOrWhiteSpace(state) ? "" : $".{state}"),
            LogChartType.ValueStackedBars => "Image/Chart.StackedBars"
                                              + (string.IsNullOrWhiteSpace(state) ? "" : $".{state}"),
            LogChartType.ValueStatisticBars
                or LogChartType.ValueBars => "Image/Chart.Bars"
                                             + (string.IsNullOrWhiteSpace(state) ? "" : $".{state}"),
            LogChartType.ValueCurves
                or LogChartType.ValueCurvesWithDataPoints => "Image/Chart.Curves"
                                                            + (string.IsNullOrWhiteSpace(state) ? "" : $".{state}"),
            LogChartType.ValueLines
                or LogChartType.ValueLinesWithDataPoints => "Image/Chart.Lines"
                                                            + (string.IsNullOrWhiteSpace(state) ? "" : $".{state}"),
            LogChartType.None => "Image/LogProfile.Empty",
            _ => null,
        };
        return key is not null 
            ? this.app.FindResourceOrDefault<IImage?>(key) 
            : null;
    }
}