namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Source of log profile icon.
/// </summary>
interface ILogProfileIconSource
{
    /// <summary>
    /// Get icon.
    /// </summary>
    LogProfileIcon Icon { get; }


    /// <summary>
    /// Get color of icon.
    /// </summary>
    LogProfileIconColor IconColor { get; }
}