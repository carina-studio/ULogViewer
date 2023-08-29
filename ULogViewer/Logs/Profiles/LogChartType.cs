namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Type of log chart.
/// </summary>
public enum LogChartType
{
    /// <summary>
    /// None,
    /// </summary>
    None,
    /// <summary>
    /// Lines of values,
    /// </summary>
    ValueLines,
    /// <summary>
    /// Lines of values with data points,
    /// </summary>
    ValueLinesWithDataPoints,
    /// <summary>
    /// Curves of values,
    /// </summary>
    ValueCurves,
    /// <summary>
    /// Curves of values with data points,
    /// </summary>
    ValueCurvesWithDataPoints,
    /// <summary>
    /// Areas of values,
    /// </summary>
    ValueAreas,
    /// <summary>
    /// Areas of values with data points,
    /// </summary>
    ValueAreasWithDataPoints,
    /// <summary>
    /// Stacked areas of values,
    /// </summary>
    ValueStackedAreas,
    /// <summary>
    /// Stacked areas of values with data points,
    /// </summary>
    ValueStackedAreasWithDataPoints,
    /// <summary>
    /// Bars of values,
    /// </summary>
    ValueBars,
    /// <summary>
    /// Stacked bars of values,
    /// </summary>
    ValueStackedBars,
    /// <summary>
    /// Bars of statistic of values,
    /// </summary>
    ValueStatisticBars,
}


/// <summary>
/// Extensions of <see cref="LogChartType"/>.
/// </summary>
static class LogChartTypeExtensions
{
    /// <summary>
    /// Check whether the type of chart is consist of series of number value from log property directly or not.
    /// </summary>
    /// <param name="type">Type of log chart.</param>
    /// <returns>True if type of chart is consist of series of number value from log property directly.</returns>
    public static bool IsDirectNumberValueSeriesType(this LogChartType type) => type switch
    {
        LogChartType.None
            or LogChartType.ValueStatisticBars => false,
        _ => true,
    };
    
    
    /// <summary>
    /// Check whether the type of chart is consist of stacked values or not.
    /// </summary>
    /// <param name="type">Type of log chart.</param>
    /// <returns>True if type of chart is consist of stacked values.</returns>
    public static bool IsStackedSeriesType(this LogChartType type) => type switch
    {
        LogChartType.ValueStackedAreas
            or LogChartType.ValueStackedBars
            or LogChartType.ValueStackedAreasWithDataPoints => true,
        _ => false,
    };
}