namespace CarinaStudio.ULogViewer;

/// <summary>
/// Mode of selecting log profile for session which has no log profile yet.
/// </summary>
enum SessionInitLogProfileSelectionMode
{
    /// <summary>
    /// Do not select log profile automatically.
    /// </summary>
    None,
    /// <summary>
    /// Select log profile automatically according to the log files to be read.
    /// </summary>
    Auto,
    /// <summary>
    /// Let user select log profile after creating new session.
    /// </summary>
    Manual,
}
