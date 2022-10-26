namespace CarinaStudio.ULogViewer;

/// <summary>
/// Policy of memory usage.
/// </summary>
enum MemoryUsagePolicy
{
    /// <summary>
    /// Balance between memory usage and performance.
    /// </summary>
    Balance,
    /// <summary>
    /// Better performance.
    /// </summary>
    BetterPerformance,
    /// <summary>
    /// Use less memory.
    /// </summary>
    LessMemoryUsage,
}
