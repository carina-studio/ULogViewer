using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Manager of <see cref="LogAnalysisScriptSet"/>.
/// </summary>
class LogAnalysisScriptSetManager : BaseProfileManager<IULogViewerApplication, LogAnalysisScriptSet>, INotifyPropertyChanged
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
        var isProVersionActivated = this.Application.ProductManager.IsProductActivated(Products.Professional);
        if (!isProVersionActivated && this.Profiles.Count >= 1)
        {
            this.Logger.LogWarning("Cannot add rule set before activating Pro version");
            return;
        }
        if (this.GetProfileOrDefault(scriptSet.Id) != null)
            scriptSet.ChangeId();
        if (!isProVersionActivated)
        {
            this.CanAddScriptSet = false;
            this.PropertyChanged?.Invoke(this, new(nameof(CanAddScriptSet)));
        }
        this.AddProfile(scriptSet);
    }


    /// <summary>
    /// Check whether at least one script set can be added or not.
    /// </summary>
    public bool CanAddScriptSet { get; private set; }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static LogAnalysisScriptSetManager Default { get => DefaultInstance ?? throw new InvalidOperationException("Not initialized yet."); }


    /// <summary>
    /// Get script set with given ID.
    /// </summary>
    /// <param name="id">ID of script set.</param>
    /// <returns>Script set with given ID or Null is script set cannot be found.</returns>
    public LogAnalysisScriptSet? GetScriptSetOrDefault(string id) =>
        base.GetProfileOrDefault(id);


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
    protected override Task<IList<string>> OnGetProfileFilesAsync()
    {
        if (this.Application.ProductManager.IsProductActivated(Products.Professional))
            return base.OnGetProfileFilesAsync();
        return Task.FromResult<IList<string>>(new string[0]);
    }


    /// <inheritdoc/>
    protected override async Task OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        if (this.Application.ProductManager.IsProductActivated(Products.Professional)
            || this.Profiles.Count == 0)
        {
            this.CanAddScriptSet = true;
            this.PropertyChanged?.Invoke(this, new(nameof(CanAddScriptSet)));
        }
    }


    /// <inheritdoc/>
    protected override Task<LogAnalysisScriptSet> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        LogAnalysisScriptSet.LoadAsync(this.Application, fileName);
    

    /// <inheritdoc/>
    protected override void OnProductStateChanged(IProductManager productManager, string productId)
    {
        base.OnProductStateChanged(productManager, productId);
        if (productId == Products.Professional 
            && productManager.TryGetProductState(productId, out var state))
        {
            if (state == ProductState.Activated)
            {
                this.ScheduleSavingProfiles();
                this.LoadProfilesAsync();
                if (!this.CanAddScriptSet)
                {
                    this.CanAddScriptSet = true;
                    this.PropertyChanged?.Invoke(this, new(nameof(CanAddScriptSet)));
                }
            }
            else if (state == ProductState.Deactivated)
            {
                foreach (var ruleSet in this.Profiles.ToArray())
                    this.RemoveProfile(ruleSet, false);
                this.CancelSavingProfiles();
                if (!this.CanAddScriptSet)
                {
                    this.CanAddScriptSet = true;
                    this.PropertyChanged?.Invoke(this, new(nameof(CanAddScriptSet)));
                }
            }
        }
    }


    /// <inheritdoc/>
    protected override Task OnSaveProfileAsync(LogAnalysisScriptSet profile, string fileName)
    {
        if (this.Application.ProductManager.IsProductActivated(Products.Professional))
            return base.OnSaveProfileAsync(profile, fileName);
        this.Logger.LogWarning($"Skip saving profile '{profile.Name}' ({profile.Id})");
        return Task.CompletedTask;
    }
    

    /// <summary>
    /// Get all script sets.
    /// </summary>
    public IReadOnlyList<LogAnalysisScriptSet> ScriptSets { get => base.Profiles; }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <inheritdoc/>
    protected override string ProfilesDirectory { get; }


    /// <summary>
    /// Remove script set.
    /// </summary>
    /// <param name="scriptSet">Script set.</param>
    /// <returns>True if script set has been removed successfully.</returns>
    public bool RemoveScriptSet(LogAnalysisScriptSet scriptSet)
    {
        var result = this.RemoveProfile(scriptSet);
        if (result
            && !this.Application.ProductManager.IsProductActivated(Products.Professional)
            && this.Profiles.Count == 0
            && !this.CanAddScriptSet)
        {
            this.CanAddScriptSet = true;
            this.PropertyChanged?.Invoke(this, new(nameof(CanAddScriptSet)));
        }
        return result;
    }
}