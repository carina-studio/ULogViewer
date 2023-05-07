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
    /// Stacked areas of values,
    /// </summary>
    ValueStackedAreas,
    /// <summary>
    /// Bars of values,
    /// </summary>
    ValueBars,
    /// <summary>
    /// Stacked bars of values,
    /// </summary>
    ValueStackedBars,
    /// <summary>
    /// Bars of categories,
    /// </summary>
    CategoryBars,
}