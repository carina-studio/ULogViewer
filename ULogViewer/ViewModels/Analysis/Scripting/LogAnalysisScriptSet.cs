using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;

/// <summary>
/// Set of script to analyze log.
/// </summary>
class LogAnalysisScriptSet : BaseProfile<IULogViewerApplication>, ILogProfileIconSource
{
    /// <summary>
    /// Options for sub scripts.
    /// </summary>
    public static readonly ScriptOptions ScriptOptions = new()
    {
        ContextType = typeof(ILogAnalysisScriptContext),
        ExtensionTypes = new HashSet<Type>()
        {
            typeof(LogAnalysisScriptContextExtensions),
            typeof(LogExtensions),
        },
        ImportedNamespaces = new HashSet<string>()
        {
            "CarinaStudio.ULogViewer.Logs",
            "CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting",
            "System.Text.RegularExpressions",
        },
        ReferencedAssemblies = new HashSet<Assembly>()
        {
            Assembly.GetExecutingAssembly()
        },
    };


    // Fields.
    IScript? analysisScript;
    LogProfileIcon icon = LogProfileIcon.Analysis;
    LogProfileIconColor iconColor = LogProfileIconColor.Default;
    IScript? setupScript;


    /// <summary>
    /// Initialize new <see cref="LogAnalysisScriptSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public LogAnalysisScriptSet(IULogViewerApplication app) : this(app, LogAnalysisScriptSetManager.Default.GenerateProfileId())
    { }


    /// <summary>
    /// Initialize new <see cref="LogAnalysisScriptSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="id">ID.</param>
    public LogAnalysisScriptSet(IULogViewerApplication app, string id) : base(app, id, false)
    { }


    /// <summary>
    /// Initialize new <see cref="LogAnalysisScriptSet"/> instance.
    /// </summary>
    /// <param name="template">Template.</param>
    /// <param name="name">Name.</param>
    public LogAnalysisScriptSet(LogAnalysisScriptSet template, string name) : base(template.Application, LogAnalysisScriptSetManager.Default.GenerateProfileId(), false)
    { 
        this.analysisScript = template.analysisScript;
        this.icon = template.icon;
        this.iconColor = template.iconColor;
        this.Name = name;
        this.setupScript = template.setupScript;
    }


    /// <summary>
    /// Script to analysis log.
    /// </summary>
    /// <remarks>Type of returned value is <see cref="bool"/>.</remarks>
    public IScript? AnalysisScript
    {
        get => this.analysisScript;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.analysisScript?.Equals(value) ?? value == null)
                return;
            this.analysisScript = value;
            this.OnPropertyChanged(nameof(AnalysisScript));
        }
    }


    // Change ID.
    internal void ChangeId() =>
        this.Id = LogAnalysisScriptSetManager.Default.GenerateProfileId();


    /// <inheritdoc/>
    public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
        object.ReferenceEquals(this, profile)
        || (profile is LogAnalysisScriptSet scriptSet
            && scriptSet.analysisScript == this.analysisScript
            && scriptSet.icon == this.icon
            && scriptSet.iconColor == this.iconColor
            && scriptSet.Name == this.Name
            && scriptSet.setupScript == this.setupScript);


    /// <summary>
    /// Get or set icon of rule sets.
    /// </summary>
    public LogProfileIcon Icon
    {
        get => this.icon;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.icon == value)
                return;
            this.icon = value;
            this.OnPropertyChanged(nameof(Icon));
        }
    }


    /// <summary>
    /// Get or set color of icon of rule sets.
    /// </summary>
    public LogProfileIconColor IconColor
    {
        get => this.iconColor;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.iconColor == value)
                return;
            this.iconColor = value;
            this.OnPropertyChanged(nameof(IconColor));
        }
    }


    // Load script set from JSON data.
    internal static LogAnalysisScriptSet Load(IULogViewerApplication app, JsonElement element)
    {
        // get ID
        var id = element.TryGetProperty(nameof(Id), out var jsonProperty) && jsonProperty.ValueKind == JsonValueKind.String
            ? jsonProperty.GetString().AsNonNull()
            : LogAnalysisScriptSetManager.Default.GenerateProfileId();
        
        // load
        var scriptSet = new LogAnalysisScriptSet(app, id);
        scriptSet.Load(element);
        return scriptSet;
    }


    /// <summary>
    /// Load script set from file.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of loading script set.</returns>
    public static async Task<LogAnalysisScriptSet> LoadAsync(IULogViewerApplication app, string fileName)
    {
        // load JSON data
        using var jsonDocument = await ProfileExtensions.IOTaskFactory.StartNew(() =>
        {
            using var reader = new StreamReader(fileName, System.Text.Encoding.UTF8);
            return JsonDocument.Parse(reader.ReadToEnd());
        });
        var element = jsonDocument.RootElement;
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Root element must be an object.");
        
        // load
        return Load(app, element);
    }


    /// <inheritdoc/>
    protected override void OnLoad(JsonElement element)
    {
        throw new NotImplementedException();
    }


     /// <inheritdoc/>
    protected override void OnSave(Utf8JsonWriter writer, bool includeId)
    {
        throw new NotImplementedException();
    }


    // Save script set in JSON format.
    internal void Save(Utf8JsonWriter writer) =>
        this.OnSave(writer, false);


    /// <summary>
    /// Script to setup analysis context.
    /// </summary>
    /// <remarks>There is no returned value from script.</remarks>
    public IScript? SetupScript
    {
        get => this.setupScript;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.setupScript?.Equals(value) ?? value == null)
                return;
            this.setupScript = value;
            this.OnPropertyChanged(nameof(SetupScript));
        }
    }
}


/// <summary>
/// Context of log analysis script.
/// </summary>
public interface ILogAnalysisScriptContext : IUserInteractiveContext
{
    /// <summary>
    /// Add analysis result.
    /// </summary>
    /// <param name="result">Result.</param>
    void AddResult(Result result);


    /// <summary>
    /// Get current log.
    /// </summary>
    ILog Log { get; }
}


/// <summary>
/// Extensions for <see cref="ILogAnalysisScriptContext"/>.
/// </summary>
public static class LogAnalysisScriptContextExtensions
{
}


/// <summary>
/// Interface for <see cref="LogAnalysisScript"/> to access log.
/// </summary>
public interface ILog
{
    /// <summary>
    /// Get property of log.
    /// </summary>
    /// <param name="name">Name of property.</param>
    /// <param name="defaultValue">Default value if there is no value got from property.</param>
    /// <returns>Value of log property.</returns>
    object? GetProperty(string name, object? defaultValue = default);


    /// <summary>
    /// Get text of log.
    /// </summary>
    string Text { get; }
}


/// <summary>
/// Empty implementation of <see cref="ILog"/>.
/// </summary>
class EmptyLog : ILog
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly ILog Default = new EmptyLog();


    // Constructor.
    EmptyLog()
    { }


    /// <inheritdoc/>
    public object? GetProperty(string name, object? defaultValue = default) =>
        defaultValue;
    

    /// <inheritdoc/>
    public string Text => "";
}


/// <summary>
/// Extensions for <see cref="ILog"/>.
/// </summary>
public static class LogExtensions
{
    /// <summary>
    /// Get property of log.
    /// </summary>
    /// <param name="log">Log.</param>
    /// <param name="name">Name of property.</param>
    /// <returns>Value of log property or Null.</returns>
    public static object? GetProperty(this ILog log, string name) =>
        log.GetProperty(name, default);


    /// <summary>
    /// Get property of log with specific type.
    /// </summary>
    /// <param name="log">Log.</param>
    /// <param name="name">Name of property.</param>
    /// <returns>Value of log property or default value.</returns>
#pragma warning disable CS8604
    public static T? GetProperty<T>(this ILog log, string name) =>
        GetProperty<T>(log, name, default);
#pragma warning restore CS8604
    

    /// <summary>
    /// Get property of log with specific type.
    /// </summary>
    /// <param name="log">Log.</param>
    /// <param name="name">Name of property.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <returns>Value of log property or default value.</returns>
    public static T? GetProperty<T>(this ILog log, string name, T defaultValue)
    {
        var rawValue = log.GetProperty(name);
        if (rawValue == null)
            return defaultValue;
        if (rawValue is T targetValue)
            return targetValue;
        return defaultValue;
    }
}


/// <summary>
/// Result of analysis by script.
/// </summary>
public struct Result
{
    /// <summary>
    /// Initialize <see cref="Result"/> structure.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <param name="message">Message.</param>
    /// <param name="log">Log.</param>
    public Result(ResultType type, string message, ILog log)
    {
        this.BeginningLog = null;
        this.ByteSize = null;
        this.Duration = null;
        this.EndingLog = null;
        this.Log = log;
        this.Message = message;
        this.Quantity = null;
        this.Type = type;
    }


    /// <summary>
    /// Initialize <see cref="Result"/> structure.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <param name="message">Message.</param>
    /// <param name="beginningLog">Beginning log.</param>
    /// <param name="endingLog">Ending log.</param>
    public Result(ResultType type, string message, ILog beginningLog, ILog endingLog)
    {
        this.BeginningLog = beginningLog;
        this.ByteSize = null;
        this.Duration = null;
        this.EndingLog = endingLog;
        this.Log = null;
        this.Message = message;
        this.Quantity = null;
        this.Type = type;
    }


    /// <summary>
    /// Beginning log.
    /// </summary>
    public ILog? BeginningLog { get; set; }


    /// <summary>
    /// Size in byte which is related to result.
    /// </summary>
    public long? ByteSize { get; set; }


    /// <summary>
    /// Duration.
    /// </summary>
    public TimeSpan? Duration { get; set; }


    /// <summary>
    /// Ending log.
    /// </summary>
    public ILog? EndingLog { get; set; }


    /// <summary>
    /// Log.
    /// </summary>
    public ILog? Log { get; set; }


    /// <summary>
    /// Message of result.
    /// </summary>
    public string? Message { get; set; }


    /// <summary>
    /// Related quantity of result.
    /// </summary>
    public long? Quantity { get; set; }


    /// <summary>
    /// Type of result.
    /// </summary>
    public ResultType Type { get; set; }
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