using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Provider of <see cref="ScriptLogDataSource"/>.
/// </summary>
class ScriptLogDataSourceProvider : BaseLogDataSourceProvider
{
    /// <summary>
    /// Options for sub scripts.
    /// </summary>
    public static readonly ScriptOptions ScriptOptions = new()
    {
        ContextType = typeof(ILogDataSourceScriptContext),
        ImportedNamespaces = new HashSet<string>()
        {
            "CarinaStudio.ULogViewer.Logs.DataSources",
            "System.IO",
        },
        ReferencedAssemblies = new HashSet<Assembly>()
        {
            Assembly.GetExecutingAssembly(),
        },
    };


    // Static fields.
    static readonly ISet<string> EmptyStringSet = new HashSet<string>().AsReadOnly();


    // Fields.
    IScript? closingReaderScript;
    string? displayName;
    IScript? openingReaderScript;
    IScript? readingLineScript;
    ISet<string> requiredSourceOptions = EmptyStringSet;
    ISet<string> supportedSourceOptions = EmptyStringSet;


    /// <summary>
    /// Initialize new <see cref=""/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public ScriptLogDataSourceProvider(IULogViewerApplication app) : this(app, GenerateName())
    { }


    /// <summary>
    /// Initialize new <see cref=""/> instance.
    /// </summary>
    /// <param name="template">Template provider.</param>
    /// <param name="displayName">Display name.</param>
    public ScriptLogDataSourceProvider(ScriptLogDataSourceProvider template, string? displayName) : this(template.Application, GenerateName())
    {
        this.closingReaderScript = template.closingReaderScript;
        this.displayName = displayName;
        this.openingReaderScript = template.openingReaderScript;
        this.readingLineScript = template.readingLineScript;
    }


    // Constructor.
    ScriptLogDataSourceProvider(IULogViewerApplication app, string name) : base(app)
    {
        this.Name = name;
    }


    /// <summary>
    /// Get or set script to close log data reader.
    /// </summary>
    public IScript? ClosingReaderScript
    {
        get => this.closingReaderScript;
        set
        {
            this.VerifyAccess();
            if (this.closingReaderScript?.Equals(value) ?? value == null)
                return;
            this.closingReaderScript = value;
            this.OnPropertyChanged(nameof(ClosingReaderScript));
        }
    }


    /// <inheritdoc/>
    protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) =>
        new ScriptLogDataSource(this, options);
    

    /// <summary>
    /// Get or set display name of provider.
    /// </summary>
    public new string? DisplayName
    {
        get => this.displayName;
        set
        {
            this.VerifyAccess();
            if (this.displayName == value)
                return;
            this.displayName = value;
            this.OnPropertyChanged(nameof(DisplayName));
        }
    }
    

    // Generate random name.
    static string GenerateName()
    {
        var r = new Random();
        var postfix = new string(new char[32].Also(it =>
        {
            for (var i = it.Length - 1; i >= 0; --i)
            {
                var n = r.Next(36);
                it[i] = n < 10 ? (char)('0' + n) : (char)('a' + (n - 10));
            }
        }));
        return $"{DateTime.UtcNow.Ticks}-{postfix}";
    }


    /// <inheritdoc/>
    public override bool IsProVersionOnly => true;


    /// <summary>
    /// Load provider from file asynchronously.
    /// </summary>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of loading provider.</returns>
    public static Task<ScriptLogDataSourceProvider> LoadAsync(string fileName)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override string Name { get; }


    /// <summary>
    /// Get or set script to open log data reader.
    /// </summary>
    public IScript? OpeningReaderScript
    {
        get => this.openingReaderScript;
        set
        {
            this.VerifyAccess();
            if (this.openingReaderScript?.Equals(value) ?? value == null)
                return;
            this.openingReaderScript = value;
            this.OnPropertyChanged(nameof(OpeningReaderScript));
        }
    }


    /// <summary>
    /// Get or set script to read raw log line.
    /// </summary>
    public IScript? ReadingLineScript
    {
        get => this.readingLineScript;
        set
        {
            this.VerifyAccess();
            if (this.readingLineScript?.Equals(value) ?? value == null)
                return;
            this.readingLineScript = value;
            this.OnPropertyChanged(nameof(ReadingLineScript));
        }
    }


    /// <inheritdoc/>
    public override ISet<string> RequiredSourceOptions => throw new NotImplementedException();


    /// <summary>
    /// Save provider to file asynchronously.
    /// </summary>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of saving provider.</returns>
    public Task SaveAsync(string fileName)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Set set of supported source options.
    /// </summary>
    /// <param name="options">Name of supported options.</param>
    /// <param name="required">Name of required options.</param>
    public void SetSupportedSourceOptions(IEnumerable<string> options, IEnumerable<string> required)
    {
    }


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions => throw new NotImplementedException();
}