using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Manager of <see cref="LogAnalysisScriptSet"/>.
/// </summary>
class LogAnalysisScriptSetManager : BaseProfileManager<IULogViewerApplication, LogAnalysisScriptSet>
{
    // Static fields.
    static LogAnalysisScriptSetManager? DefaultInstance;


    // Constructor.
    LogAnalysisScriptSetManager(IULogViewerApplication app) : base(app)
    { 
        this.ProfilesDirectory = Path.Combine(app.RootPrivateDirectoryPath, "LogAnalysisScripts");
    }


    /// <summary>
    /// Add script set.
    /// </summary>
    /// <param name="scriptSet">Script set.</param>
    public void AddScriptSet(LogAnalysisScriptSet scriptSet)
    {
        this.VerifyAccess();
        if (scriptSet.Manager != null)
            throw new InvalidOperationException();
        if (this.GetProfileOrDefault(scriptSet.Id) != null)
            scriptSet.ChangeId();
        this.AddProfile(scriptSet);
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static LogAnalysisScriptSetManager Default { get => DefaultInstance ?? throw new InvalidOperationException("Not initialized yet."); }


    /// <summary>
    /// Initialize asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static async Task InitializeAsync(IULogViewerApplication app)
    {
        // check state
        app.VerifyAccess();
        if (DefaultInstance != null)
            throw new InvalidOperationException();
        
        // create and initialize
        DefaultInstance = new(app);
        await DefaultInstance.WaitForInitialization();
    }


    /// <inheritdoc/>
    protected override Task<LogAnalysisScriptSet> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        LogAnalysisScriptSet.LoadAsync(this.Application, fileName);
    

    /// <summary>
    /// Get all script sets.
    /// </summary>
    public IReadOnlyList<LogAnalysisScriptSet> ScriptSets { get => base.Profiles; }


    /// <inheritdoc/>
    protected override string ProfilesDirectory { get; }


    /// <summary>
    /// Remove script set.
    /// </summary>
    /// <param name="scriptSet">Script set.</param>
    /// <returns>True if script set has been removed successfully.</returns>
    public bool RemoveScriptSet(LogAnalysisScriptSet scriptSet) =>
        base.RemoveProfile(scriptSet);
}