namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Format of log file.
/// </summary>
enum LogFileFormat
{
    /// <summary>
    /// Format is unknown.
    /// </summary>
    Unknown,
    /// <summary>
    /// Plain text.
    /// </summary>
    PlainText,
    /// <summary>
    /// JSON data.
    /// </summary>
    Json,
    /// <summary>
    /// Compact Log Event Format (CLEF) data.
    /// </summary>
    Clef,
    /// <summary>
    /// Windows event log file.
    /// </summary>
    WindowsEventLog,
    /// <summary>
    /// Binary data which is not readable as text.
    /// </summary>
    Binary,
}
