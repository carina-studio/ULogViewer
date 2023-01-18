namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Interface to access internal state of <see cref="Session"/>.
/// </summary>
interface ISessionInternalAccessor
{
    /// <summary>
    /// Get group of displayable logs.
    /// </summary>
    DisplayableLogGroup? DisplayableLogGroup { get; }


    /// <summary>
    /// Get memory usage policy.
    /// </summary>
    MemoryUsagePolicy MemoryUsagePolicy { get; }
}