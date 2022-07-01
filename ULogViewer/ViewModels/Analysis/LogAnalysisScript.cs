using CarinaStudio.ULogViewer.Scripting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Script for log analysis.
/// </summary>
class LogAnalysisScript : Script<ILogAnalysisScriptContext>
{
    // Static fields.
    static readonly List<string> _Namespaces = new List<string>()
    {
        "CarinaStudio.ULogViewer.Logs",
        "CarinaStudio.ULogViewer.ViewModels.Analysis",
        "System.Text.RegularExpressions",
    };
    static readonly List<Assembly> _References = new List<Assembly>().Also(it =>
    {
    });


    /// <summary>
    /// Initialize new <see cref="LogAnalysisScript"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="language">Language.</param>
    /// <param name="source">Source.</param>
    public LogAnalysisScript(IULogViewerApplication app, ScriptLanguage language, string source) : base(app, language, source)
    { }


    /// <summary>
    /// Load script from JSON format data.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="json">JSON data.</param>
    /// <returns>Loaded script.</returns>
    public static LogAnalysisScript Load(IULogViewerApplication app, JsonElement json) =>
        LogAnalysisScript.Load<LogAnalysisScript>(app, json);


    /// <inheritdoc/>
    public override IList<string> Namespaces => _Namespaces;


    /// <inheritdoc/>
    public override IList<Assembly> References => _References;
}


/// <summary>
/// Context of log analysis script.
/// </summary>
public interface ILogAnalysisScriptContext : IContext
{
    /// <summary>
    /// Add analysis result.
    /// </summary>
    /// <param name="type">Type of result.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    void AddResult(ResultType type, string message, ILogAnalysisScriptLog? log);


    /// <summary>
    /// Add analysis result.
    /// </summary>
    /// <param name="type">Type of result.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    void AddResult(ResultType type, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog);


    /// <summary>
    /// Get current log.
    /// </summary>
    ILogAnalysisScriptLog Log { get; }
}


/// <summary>
/// Interface for <see cref="LogAnalysisScript"/> to access log.
/// </summary>
public interface ILogAnalysisScriptLog
{
    /// <summary>
    /// Get property of log.
    /// </summary>
    /// <param name="name">Name of property.</param>
    /// <param name="defaultValue">Default value if there is no value got from property.</param>
    /// <returns>Value of log property.</returns>
    T? GetProperty<T>(string name, T? defaultValue = default);


    /// <summary>
    /// Get text of log.
    /// </summary>
    string Text { get; }
}


/// <summary>
/// Empty implementation of <see cref="ILogAnalysisScriptLog"/>.
/// </summary>
class EmptyLogAnalysisScriptLog : ILogAnalysisScriptLog
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly ILogAnalysisScriptLog Default = new EmptyLogAnalysisScriptLog();


    // Constructor.
    EmptyLogAnalysisScriptLog()
    { }


    /// <inheritdoc/>
    public T? GetProperty<T>(string name, T? defaultValue = default) =>
        defaultValue;
    

    /// <inheritdoc/>
    public string Text => "";
}


/// <summary>
/// Type of analysis result exported to script.
/// </summary>
public enum ResultType
{
    /// <summary>
    /// Error.
    /// </summary>
    Error = (int)DisplayableLogAnalysisResultType.Error,
    /// <summary>
    /// Warning.
    /// </summary>
    Warning = (int)DisplayableLogAnalysisResultType.Warning,
    /// <summary>
    /// Start of operation.
    /// </summary>
    OperationStart = (int)DisplayableLogAnalysisResultType.OperationStart,
    /// <summary>
    /// End of operation.
    /// </summary>
    OperationEnd = (int)DisplayableLogAnalysisResultType.OperationEnd,
    /// <summary>
    /// Checkpoint.
    /// </summary>
    Checkpoint = (int)DisplayableLogAnalysisResultType.Checkpoint,
    /// <summary>
    /// Time span.
    /// </summary>
    TimeSpan = (int)DisplayableLogAnalysisResultType.TimeSpan,
    /// <summary>
    /// Performance.
    /// </summary>
    Performance = (int)DisplayableLogAnalysisResultType.Performance,
    /// <summary>
    /// Information.
    /// </summary>
    Information = (int)DisplayableLogAnalysisResultType.Information,
}