using System;

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
    /// Raised when icon or related property changed.
    /// </summary>
    event EventHandler? IconChanged;


    /// <summary>
    /// Get color of icon.
    /// </summary>
    LogProfileIconColor IconColor { get; }
}