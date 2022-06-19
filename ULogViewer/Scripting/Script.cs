using System;
using System.Collections.Generic;
using System.IO;
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
class Script<TContext> where TContext : ScriptContext, IEquatable<Script<TContext>>
{
    // Fields.
    readonly string hashCodeSource;


    /// <summary>
    /// Initialize new <see cref="Script{TContext, TResult}"/> instance.
    /// </summary>
    /// <param name="language">Language.</param>
    /// <param name="source">Source code.</param>
    public Script(ScriptLanguage language, string source)
    {
        this.hashCodeSource = source.Length <= 32 ? source : source.Substring(0, 32);
        this.Language = language;
        this.Source = source;
    }


    /// <inheritdoc/>
    public bool Equals(Script<TContext>? script) =>
        script != null
        && this.Language == this.Language
        && this.Source == this.Source;


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Script<TContext> script
        && this.Equals(script);


    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.hashCodeSource.GetHashCode();


    /// <summary>
    /// Get language of script.
    /// </summary>
    public ScriptLanguage Language { get; }


    /// <summary>
    /// Load script from file asynchronously.
    /// </summary>
    /// <param name="fileName">File name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of loading script.</returns>
    public static async Task<Script<TContext>> LoadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new TaskCanceledException();
        var language = ScriptLanguage.CSharp;
        var source = "";
        await Task.Run(() =>
        {
            if (!CarinaStudio.IO.File.TryOpenRead(fileName, 5000, out var stream) || stream == null)
                throw new IOException($"Unable to open file '{fileName}'.");
            using var jsonDocument = JsonDocument.Parse(stream);
            var jsonScript = jsonDocument.RootElement;
            if (jsonScript.ValueKind != JsonValueKind.Object)
                throw new JsonException("Root element should be an object.");
            if (jsonScript.TryGetProperty(nameof(Language), out var jsonValue)
                && jsonValue.ValueKind == JsonValueKind.String)
            {
                Enum.TryParse<ScriptLanguage>(jsonValue.GetString(), out language);
            }
            source = Encoding.UTF8.GetString(Convert.FromBase64String(jsonScript.GetProperty(nameof(Source)).GetString().AsNonNull()));
        });
        if (cancellationToken.IsCancellationRequested)
            throw new TaskCanceledException();
        return new(language, source);
    }


    /// <summary>
    /// Run script asynchronously.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="references">Extra reference assemblies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of running script.</returns>
    public Task RunAsync(TContext context, IEnumerable<Assembly> references, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Run script asynchronously.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="references">Extra reference assemblies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="TResult">Type of result.</typeparam>
    /// <returns>Task of running script.</returns>
    public Task<TResult> RunAsync<TResult>(TContext context, IEnumerable<Assembly> references, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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
        writer.WriteStartObject();
        writer.WriteString(nameof(Language), this.Language.ToString());
        writer.WriteString(nameof(Source), Convert.ToBase64String(Encoding.UTF8.GetBytes(this.Source)));
        writer.WriteEndObject();
    });


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
    /// <param name="ex">Inner exception.</param>
    public ScriptException(string message, Exception ex) : base(message, ex)
    { }
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
}