using CarinaStudio.Collections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Script.
/// </summary>
/// <typeparam name="TContext">Type of context.</typeparam>
class Script<TContext> : IEquatable<Script<TContext>> where TContext : IContext
{
    // Static logger.
    static volatile int NextId = 0;


    // Fields.
    readonly IULogViewerApplication app;
    readonly string hashCodeSource;
    readonly ILogger logger;


    /// <summary>
    /// Initialize new <see cref="Script{TContext, TResult}"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="language">Language.</param>
    /// <param name="source">Source code.</param>
    protected Script(IULogViewerApplication app, ScriptLanguage language, string source)
    {
        this.app = app;
        this.hashCodeSource = source.Length <= 32 ? source : source.Substring(0, 32);
        this.Id = Interlocked.Increment(ref NextId);
        this.Language = language;
        this.logger = App.Current.LoggerFactory.CreateLogger($"Script-{this.Id}");
        this.Source = source;
    }


    /// <summary>
    /// Get latest compilation results.
    /// </summary>
    public IReadOnlyList<CompilationResult> CompilationResults { get; private set; } = (IReadOnlyList<CompilationResult>)new CompilationResult[0].AsReadOnly();


    /// <summary>
    /// Compile script asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of compilation.</returns>
    public Task<bool> CompileAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public virtual bool Equals(Script<TContext>? script) =>
        object.ReferenceEquals(this, script)
        || (script != null
            && script.GetType().Equals(this.GetType())
            && script.Language == this.Language
            && script.Source == this.Source);


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Script<TContext> script
        && this.Equals(script);
    

    /// <summary>
    /// Get list of type to provide extension methods to allow accessing by script.
    /// </summary>
    protected virtual IEnumerable<Type> ExtensionMethodProviders { get; } = new Type[0];


    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.hashCodeSource.GetHashCode();
    

    /// <summary>
    /// Check whether error was occurred or not.
    /// </summary>
    public bool HasError { get; private set; }
    

    /// <summary>
    /// Get unique ID of script instance.
    /// </summary>
    public int Id { get; }


    /// <summary>
    /// Get language of script.
    /// </summary>
    public ScriptLanguage Language { get; }


    /// <summary>
    /// Load script from JSON format data.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="json">JSON data.</param>
    /// <typeparam name="TScript">Type of script.</typeparam>
    /// <returns>Loaded script.</returns>
    public static TScript Load<TScript>(IULogViewerApplication app, JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            throw new JsonException("Element should be an object.");
        var language = json.TryGetProperty(nameof(Language), out var jsonValue)
            && jsonValue.ValueKind == JsonValueKind.String
            && Enum.TryParse<ScriptLanguage>(jsonValue.GetString(), out var candLanguage)
                ? candLanguage
                : ScriptLanguage.JavaScript;
        var source = Encoding.UTF8.GetString(Convert.FromBase64String(json.GetProperty(nameof(Source)).GetString().AsNonNull()));
        return (TScript)Activator.CreateInstance(typeof(TScript), app, language, source).AsNonNull();
    }


    /// <summary>
    /// Load script from file asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="TScript">Type of script.</typeparam>
    /// <returns>Task of loading script.</returns>
    public static async Task<TScript> LoadAsync<TScript>(IULogViewerApplication app, string fileName, CancellationToken cancellationToken = default) where TScript : Script<TContext>
    {
        if (cancellationToken.IsCancellationRequested)
            throw new TaskCanceledException();
        var json = default(JsonElement);
        await Task.Run(() =>
        {
            if (!CarinaStudio.IO.File.TryOpenRead(fileName, 5000, out var stream) || stream == null)
                throw new IOException($"Unable to open file '{fileName}'.");
            json = JsonDocument.Parse(stream).RootElement;
        });
        if (cancellationToken.IsCancellationRequested)
            throw new TaskCanceledException();
        return Load<TScript>(app, json);
    }


    /// <summary>
    /// Get list of namespaces to be imported.
    /// </summary>
    public virtual IList<string> Namespaces { get; } = new string[0];


    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Script<TContext>? x, Script<TContext>? y) =>
        object.ReferenceEquals(x, null) ? object.ReferenceEquals(y, null) : x.Equals(y);
    

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Script<TContext>? x, Script<TContext>? y) =>
        object.ReferenceEquals(x, null) ? !object.ReferenceEquals(y, null) : !x.Equals(y);


    /// <summary>
    /// Get list of referenced assemblies.
    /// </summary>
    public virtual IList<Assembly> References { get; } = new Assembly[0];


    /// <summary>
    /// Run script.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    public void Run(TContext context, int timeout = Timeout.Infinite)
    {
        if (!this.RunAsync(context).Wait(timeout))
            throw new TimeoutException();
    }
    

    /// <summary>
    /// Run script.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <typeparam name="TResult">Type of result.</typeparam>
    /// <returns>Result.</returns>
    public TResult Run<TResult>(TContext context, int timeout = Timeout.Infinite)
    {
        var task = this.RunAsync<TResult>(context);
        if (!task.Wait(timeout))
            throw new TimeoutException();
        return task.Result;
    }


    /// <summary>
    /// Run script asynchronously.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of running script.</returns>
    public Task RunAsync(TContext context, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Run script asynchronously.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="TResult">Type of result.</typeparam>
    /// <returns>Task of running script.</returns>
    public Task<TResult> RunAsync<TResult>(TContext context, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Save script in JSON format data.
    /// </summary>
    /// <param name="writer">JSON writer.</param>
    public void Save(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(Language), this.Language.ToString());
        writer.WriteString(nameof(Source), Convert.ToBase64String(Encoding.UTF8.GetBytes(this.Source)));
        writer.WriteEndObject();
    }


    /// <summary>
    /// Save script to file asynchronously.
    /// </summary>
    /// <param name="fileName">File name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of saving script.</returns>
    public Task SaveAsync(string fileName, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        if (cancellationToken.IsCancellationRequested)
            throw new TaskCanceledException();
        if (!CarinaStudio.IO.File.TryOpenWrite(fileName, 5000, out var stream) || stream == null)
            throw new IOException($"Unable to open file '{fileName}'.");
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true });
        this.Save(writer);
    });


    // Setup script if needed.
    void Setup()
    {
    }


    /// <summary>
    /// Get source code of script.
    /// </summary>
    public string Source { get; }
}


/// <summary>
/// Exception of running script.
/// </summary>
class ScriptException : Exception
{
    /// <summary>
    /// Initialize new <see cref="ScriptException"/> instance.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="line">Line number of source which causes the exception.</param>
    /// <param name="column">Position of source in line which causes the exception.</param>
    /// <param name="ex">Inner exception.</param>
    public ScriptException(string message, int line, int column, Exception ex) : base(message, ex)
    {
        this.Column = column;
        this.Line = line;
    }


    /// <summary>
    /// Initialize new <see cref="ScriptException"/> instance.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="ex">Inner exception.</param>
    public ScriptException(string message, Exception ex) : base(message, ex)
    { }


    /// <summary>
    /// Initialize new <see cref="ScriptException"/> instance.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="line">Line number of source which causes the exception.</param>
    /// <param name="column">Position of source in line which causes the exception.</param>
    public ScriptException(string message, int line, int column) : base(message)
    { 
        this.Column = column;
        this.Line = line;
    }


    /// <summary>
    /// Initialize new <see cref="ScriptException"/> instance.
    /// </summary>
    /// <param name="message">Message.</param>
    public ScriptException(string message) : base(message)
    { }


    /// <summary>
    /// Get position of source in line which causes the exception.
    /// </summary>
    public int Column { get; }


    /// <summary>
    /// Get line number of source which causes the exception.
    /// </summary>
    public int Line { get; }
}


/// <summary>
/// Language of script.
/// </summary>
enum ScriptLanguage
{
    /// <summary>
    /// C# script.
    /// </summary>
    CSharp,
    /// <summary>
    /// JavaScript (ECMAScript 5.1).
    /// </summary>
    JavaScript,
}


/// <summary>
/// Result of script compilation.
/// </summary>
class CompilationResult
{
    /// <summary>
    /// Get (line, column) of end position.
    /// </summary>
    public (int, int)? EndPosition { get; }


    /// <summary>
    /// Get message of result.
    /// </summary>
    public string? Message { get; }


    /// <summary>
    /// Get (line, column) of start position.
    /// </summary>
    public (int, int)? StartPosition { get; }


    /// <summary>
    /// Get type of result.
    /// </summary>
    public CompilationResultType Type { get; }
}


/// <summary>
/// Type of result of script compilation.
/// </summary>
enum CompilationResultType
{
    /// <summary>
    /// Information.
    /// </summary>
    Information,
    /// <summary>
    /// Warning.
    /// </summary>
    Warning,
    /// <summary>
    /// Error.
    /// </summary>
    Error,
}