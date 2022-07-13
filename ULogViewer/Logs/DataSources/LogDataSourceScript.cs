using CarinaStudio.ULogViewer.Scripting;
using System.Collections.Generic;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Script for log data source.
/// </summary>
class LogDataSourceScript : Script<ILogDataSourceScriptContext>
{
    // Static fields.
    static readonly List<string> _Namespaces = new List<string>()
    {
        "System.IO",
    };


    /// <summary>
    /// Initialize new <see cref="LogDataSourceScript"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="language">Language.</param>
    /// <param name="source">Source.</param>
    public LogDataSourceScript(IULogViewerApplication app, ScriptLanguage language, string source) : base(app, language, source)
    { }


    /// <summary>
    /// Load script from JSON format data.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="json">JSON data.</param>
    /// <returns>Loaded script.</returns>
    public static LogDataSourceScript Load(IULogViewerApplication app, JsonElement json) =>
        LogDataSourceScript.Load<LogDataSourceScript>(app, json);
    

    /// <inheritdoc/>
    public override IList<string> Namespaces => _Namespaces;
}


/// <summary>
/// <see cref="IContext"/> for log data source script.
/// </summary>
public interface ILogDataSourceScriptContext : IContext
{
    /// <summary>
    /// Get options of data source.
    /// </summary>
    LogDataSourceOptions Options { get; }
}