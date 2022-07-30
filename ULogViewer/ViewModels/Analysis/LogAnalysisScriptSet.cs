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

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Set of script to analyze log.
/// </summary>
class LogAnalysisScriptSet : BaseProfile<IULogViewerApplication>
{
    /// <summary>
    /// Options for sub scripts.
    /// </summary>
    public static readonly ScriptOptions ScriptOptions = new()
    {
        ContextType = null,
        ExtensionTypes = new HashSet<Type>()
        {
            typeof(LogAnalysisScriptContextExtensions),
            typeof(LogAnalysisScriptLogExtensions),
        },
        ImportedNamespaces = new HashSet<string>()
        {
            "CarinaStudio.ULogViewer.Logs",
            "CarinaStudio.ULogViewer.ViewModels.Analysis",
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
        
        // get ID
        var id = element.TryGetProperty(nameof(Id), out var jsonProperty) && jsonProperty.ValueKind == JsonValueKind.String
            ? jsonProperty.GetString().AsNonNull()
            : KeyLogAnalysisRuleSetManager.Default.GenerateProfileId();
        
        // load
        var scriptSet = new LogAnalysisScriptSet(app, id);
        scriptSet.Load(element);
        return scriptSet;
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