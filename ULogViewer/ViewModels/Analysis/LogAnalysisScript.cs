using CarinaStudio.ULogViewer.Scripting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Script for log analysis.
/// </summary>
class LogAnalysisScript : Script<ILogAnalysisScriptContext>
{
    // Static fields.
    static readonly List<Type> _ExtensionMethodProviders = new()
    {
        typeof(LogAnalysisScriptContextExtensions),
        typeof(LogAnalysisScriptLogExtensions),
    };
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


    /// <inheritdoc/>
    protected override IEnumerable<Type> ExtensionMethodProviders => _ExtensionMethodProviders;


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
/// Extensions for <see cref="ILogAnalysisScriptContext"/>.
/// </summary>
public static class LogAnalysisScriptContextExtensions
{
    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Checkpoint"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddCheckpointResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.Checkpoint, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Checkpoint"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddCheckpointResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.Checkpoint, message, beginningLog, endingLog);


    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Error"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddErrorResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.Error, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Error"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddErrorResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.Error, message, beginningLog, endingLog);


    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Information"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddInformationResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.Information, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Information"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddInformationResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.Information, message, beginningLog, endingLog);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.OperationEnd"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddOperationEndResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.OperationEnd, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.OperationEnd"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddOperationEndResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.OperationEnd, message, beginningLog, endingLog);

    
    /// <summary>
    /// Add analysis result with type <see cref="ResultType.OperationStart"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddOperationStartResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.OperationStart, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.OperationStart"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddOperationStartResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.OperationStart, message, beginningLog, endingLog);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Performance"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddPerformanceResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.Performance, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Performance"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddPerformanceResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.Performance, message, beginningLog, endingLog);


    /// <summary>
    /// Add analysis result with type <see cref="ResultType.TimeSpan"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddTimeSpanResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.TimeSpan, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.TimeSpan"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddTimeSpanResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.TimeSpan, message, beginningLog, endingLog);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Warning"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Related log.</param>
    public static void AddWarningResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog? log) =>
        context.AddResult(ResultType.Warning, message, log);
    

    /// <summary>
    /// Add analysis result with type <see cref="ResultType.Warning"/>.
    /// </summary>
    /// <param name="context"><see cref="ILogAnalysisScriptContext"/>.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public static void AddWarningResult(this ILogAnalysisScriptContext context, string message, ILogAnalysisScriptLog beginningLog, ILogAnalysisScriptLog endingLog) =>
        context.AddResult(ResultType.Warning, message, beginningLog, endingLog);
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
/// Extensions for <see cref="LogAnalysisScriptLog"/>.
/// </summary>
public static class LogAnalysisScriptLogExtensions
{
    /// <summary>
    /// Get property of log.
    /// </summary>
    /// <param name="name">Name of property.</param>
    /// <returns>Value of log property or Null.</returns>
    public static object? GetPropertyOrNull(this ILogAnalysisScriptLog log, string name) =>
        log.GetProperty<object?>(name, null);
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