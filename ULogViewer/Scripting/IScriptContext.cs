using System.Collections.Generic;
using System.Threading;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Context for running script.
/// </summary>
public interface IScriptContext
{
    /// <summary>
    /// Get data for running script.
    /// </summary>
    IDictionary<string, object> Data { get; }


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
/// Extensions for <see cref="IScriptContext"/>.
/// </summary>
public static class ScriptContextExtensions
{
    /// <summary>
    /// Get formatted string defined in application resource.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="key">Key of string.</param>
    /// <param name="args">Arguments to format string.</param>
    /// <returns>Formatted string or Null if string not found.</returns>
    public static string? GetFormattedString(this IScriptContext context, string key, params object?[] args)
    {
        var format = context.GetString(key, null);
        if (format != null)
            return string.Format(format, args);
        return null;
    }
}