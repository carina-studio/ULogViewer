namespace CarinaStudio.ULogViewer.Logs;

/// <summary>
/// Mode of matching raw log line with log patterns.
/// </summary>
enum LogPatternMatchingMode
{
    /// <summary>
    /// Match patterns sequentially.
    /// </summary>
    Sequential,
    /// <summary>
    /// Match patterns in arbitrary order.
    /// </summary>
    Arbitrary,
    /// <summary>
    /// Match patterns in arbitrary order after matching the first pattern.
    /// </summary>
    ArbitraryAfterFirstMatch,
}