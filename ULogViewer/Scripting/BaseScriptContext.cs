using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Base implementation of <see cref="IScriptContext"/>.
/// </summary>
abstract class BaseScriptContext : IScriptContext
{
    // Fields.
    readonly IDictionary<string, object> data = new ConcurrentDictionary<string, object>();
    readonly IULogViewerApplication app;


    /// <summary>
    /// Initialize new <see cref="BaseScriptContext"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public BaseScriptContext(IULogViewerApplication app)
    {
        this.app = app;
    }


    /// <summary>
    /// Get data for running script.
    /// </summary>
    public IDictionary<string, object> Data { get => this.data; }


    /// <summary>
    /// Get string defined in application resource.
    /// </summary>
    /// <param name="key">Key of string.</param>
    /// <param name="defaultString">Default string.</param>
    /// <returns>String defined in resource or <paramref name="defaultString"/> if string not found.</returns>
    public string? GetString(string key, string? defaultString) =>
        app.GetString(key, defaultString);
    

    /// <summary>
    /// Check whether current thread is main thread of application or not.
    /// </summary>
    public bool IsMainThread { get => app.CheckAccess(); }
    

    /// <summary>
    /// Get <see cref="SynchronizationContext"/> of main thread of application.
    /// </summary>
    public SynchronizationContext MainThreadSynchronizationContext { get => app.SynchronizationContext; }
}