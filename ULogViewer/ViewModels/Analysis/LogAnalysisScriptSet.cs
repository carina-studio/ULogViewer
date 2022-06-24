using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Set of script to analyze log.
/// </summary>
class LogAnalysisScriptSet : BaseProfile<IULogViewerApplication>
{
    // Fields.
    LogAnalysisScript? analysisScript;
    LogProfileIcon icon = LogProfileIcon.Analysis;
    LogAnalysisScript? setupScript;
    LogAnalysisScript? teardownScript;


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
    /// Script to analysis log.
    /// </summary>
    /// <remarks>Type of returned value is <see cref="bool"/>.</remarks>
    public LogAnalysisScript? AnalysisScript
    {
        get => this.analysisScript;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.analysisScript == value)
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
        profile is LogAnalysisScriptSet scriptSet
        && scriptSet.analysisScript == this.analysisScript
        && scriptSet.icon == this.icon
        && scriptSet.Name == this.Name
        && scriptSet.setupScript == this.setupScript
        && scriptSet.teardownScript == this.teardownScript;


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
    public LogAnalysisScript? SetupScript
    {
        get => this.setupScript;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.setupScript == value)
                return;
            this.setupScript = value;
            this.OnPropertyChanged(nameof(SetupScript));
        }
    }


    /// <summary>
    /// Script to teardown analysis context.
    /// </summary>
    /// <remarks>There is no returned value from script.</remarks>
    public LogAnalysisScript? TeardownScript
    {
        get => this.teardownScript;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.teardownScript == value)
                return;
            this.teardownScript = value;
            this.OnPropertyChanged(nameof(TeardownScript));
        }
    }
}