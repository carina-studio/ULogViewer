namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Type of X axis of log chart.
/// </summary>
enum LogChartXAxisType
{
    /// <summary>
    /// No X axis.
    /// </summary>
    None,
    /// <summary>
    /// Timestamp with simple format.
    /// </summary>
    SimpleTimestamp,
    /// <summary>
    /// Timestamp.
    /// </summary>
    Timestamp,
}