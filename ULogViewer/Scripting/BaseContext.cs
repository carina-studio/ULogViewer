using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Base implementation of <see cref="IContext"/>.
/// </summary>
abstract class BaseContext : IContext
{
    // Fields.
    readonly IDictionary<string, object> data = new ConcurrentDictionary<string, object>();
    readonly IULogViewerApplication app;


    /// <summary>
    /// Initialize new <see cref="BaseContext"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="name">Name of context.</param>
    public BaseContext(IULogViewerApplication app, string name)
    {
        this.app = app;
        this.Logger = app.LoggerFactory.CreateLogger(string.IsNullOrWhiteSpace(name) ? this.GetType().Name : name);
    }


    /// <summary>
    /// Get data for running script.
    /// </summary>
    public IDictionary<string, object> Data { get => this.data; }


    /// <inheritdoc/>
    public ILogger Logger { get; }
}