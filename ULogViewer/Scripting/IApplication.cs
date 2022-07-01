using System.Threading;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Interface for script to access application functions.
/// </summary>
public interface IApplication
{
    /// <summary>
    /// Get string defined in application resource.
    /// </summary>
    /// <param name="key">Key of string.</param>
    /// <param name="defaultString">Default string.</param>
    /// <returns>String defined in resource or <paramref name="defaultString"/> if string not found.</returns>
    string? GetString(string key, string? defaultString);


    /// <summary>
    /// Check whether current thread is main thread of application or not.
    /// </summary>
    bool IsMainThread { get; }


    /// <summary>
    /// Get <see cref="SynchronizationContext"/> of main thread of application.
    /// </summary>
    SynchronizationContext MainThreadSynchronizationContext { get; }
}


/// <summary>
/// Extensions for <see cref="IApplication"/>.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Get formatted string defined in application resource.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="key">Key of string.</param>
    /// <param name="args">Arguments to format string.</param>
    /// <returns>Formatted string or Null if string not found.</returns>
    public static string? GetFormattedString(this IApplication app, string key, params object?[] args)
    {
        var format = app.GetString(key, null);
        if (format != null)
            return string.Format(format, args);
        return null;
    }


    /// <summary>
    /// Get string defined in application resource.
    /// </summary>
    /// <param name="key">Key of string.</param>
    /// <returns>String defined in resource or Null if string not found.</returns>
    public static string? GetString(this IApplication app, string key) =>
        app.GetString(key, null);
    

    /// <summary>
    /// Get non-null string defined in application resource.
    /// </summary>
    /// <param name="key">Key of string.</param>
    /// <returns>String defined in resource or <see cref="String.Empty"/> if string not found.</returns>
    public static string GetStringNonNull(this IApplication app, string key) =>
        GetStringNonNull(app, key, "");
    

    /// <summary>
    /// Get non-null string defined in application resource.
    /// </summary>
    /// <param name="key">Key of string.</param>
    /// <param name="defaultString">Default string.</param>
    /// <returns>String defined in resource or <paramref name="defaultString"/> if string not found.</returns>
    public static string GetStringNonNull(this IApplication app, string key, string defaultString) =>
        app.GetString(key, null) ?? defaultString ?? "";
}