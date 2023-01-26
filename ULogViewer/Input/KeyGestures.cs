using Avalonia.Input;

namespace CarinaStudio.ULogViewer.Input;

/// <summary>
/// Predefined <see cref="KeyGesture"/>s.
/// </summary>
static class KeyGestures
{
    // Constants.
    static readonly KeyModifiers PrimaryKeyModifiers = Platform.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control;


    /// <summary>
    /// Close tab.
    /// </summary>
    public static readonly KeyGesture CloseTab = new(Key.W, PrimaryKeyModifiers);
    /// <summary>
    /// Copy logs with file names.
    /// </summary>
    public static readonly KeyGesture CopyLogsWithFileNames = new(Key.C, PrimaryKeyModifiers | KeyModifiers.Shift);
    /// <summary>
    /// Mark logs.
    /// </summary>
    public static readonly KeyGesture MarkLogs = new(Key.M, PrimaryKeyModifiers);
    /// <summary>
    /// Mark logs with blue color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithBlue = new(Key.D5, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with green color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithGreen = new(Key.D4, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with indigo color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithIndigo = new(Key.D6, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with magenta color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithMagenta = new(Key.D8, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with orange color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithOrange = new(Key.D2, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with purple color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithPurple = new(Key.D7, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with red color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithRed = new(Key.D1, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Mark logs with yellow color.
    /// </summary>
    public static readonly KeyGesture MarkLogsWithYellow = new(Key.D3, PrimaryKeyModifiers | KeyModifiers.Alt);
    /// <summary>
    /// Create new window.
    /// </summary>
    public static readonly KeyGesture NewWindow = new(Key.N, PrimaryKeyModifiers);
    /// <summary>
    /// Save logs.
    /// </summary>
    public static readonly KeyGesture SaveLogs = new(Key.S, PrimaryKeyModifiers);
    /// <summary>
    /// Save all logs.
    /// </summary>
    public static readonly KeyGesture SaveAllLogs = new(Key.S, PrimaryKeyModifiers | KeyModifiers.Shift);
    /// <summary>
    /// Select mark logs.
    /// </summary>
    public static readonly KeyGesture SelectMarkLogs = new(Key.S);
    /// <summary>
    /// Unmark logs.
    /// </summary>
    public static readonly KeyGesture UnmarkLogs = new(Key.D0, PrimaryKeyModifiers | KeyModifiers.Alt);
}