using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Manager of <see cref="OperationDurationAnalysisRuleSet"/>.
/// </summary>
class OperationDurationAnalysisRuleSetManager : BaseProfileManager<IULogViewerApplication, OperationDurationAnalysisRuleSet>
{
    // Fields.
    static OperationDurationAnalysisRuleSetManager? DefaultInstance;


    // Constructor.
    OperationDurationAnalysisRuleSetManager(IULogViewerApplication app) : base(app)
    { }


    /// <summary>
    /// Add rule set.
    /// </summary>
    /// <param name="rule">Rule to add.</param>
    public void AddRuleSet(OperationDurationAnalysisRuleSet rule)
    {
        this.VerifyAccess();
        if (rule.Manager != null)
            throw new InvalidOperationException();
        var isProVersionActivated = this.Application.ProductManager.IsProductActivated(Products.Professional);
        if (!isProVersionActivated && this.Profiles.Count >= 1)
        {
            this.Logger.LogWarning("Cannot add rule set before activating Pro version");
            return;
        }
        if (this.GetProfileOrDefault(rule.Id) != null)
            rule.ChangeId();
        if (!isProVersionActivated)
        {
            this.CanAddRuleSet = false;
            this.OnPropertyChanged(new(nameof(CanAddRuleSet)));
        }
        this.AddProfile(rule);
    }


    /// <summary>
    /// Check whether at least one rule set can be added or not.
    /// </summary>
    public bool CanAddRuleSet { get; private set; }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static OperationDurationAnalysisRuleSetManager Default { get => DefaultInstance ?? throw new InvalidOperationException(); }


    /// <summary>
    /// Initialize asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static async Task InitializeAsync(IULogViewerApplication app)
    {
        app.VerifyAccess();
        if (DefaultInstance != null)
            throw new InvalidOperationException();
        DefaultInstance = new(app);
        await DefaultInstance.WaitForInitialization();
    }


    /// <summary>
    /// Get rule set with given ID.
    /// </summary>
    /// <param name="id">ID of rule set.</param>
    /// <returns>Rule set with given ID or Null if rule cannot be found.</returns>
    public OperationDurationAnalysisRuleSet? GetRuleSetOrDefault(string id) =>
        this.GetProfileOrDefault(id);
    

    /// <inheritdoc/>
    protected override void OnAttachToProfile(OperationDurationAnalysisRuleSet profile)
    {
        base.OnAttachToProfile(profile);
        if (profile.IsDataUpgraded)
            this.ScheduleSavingProfile(profile);
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
            this.CanAddRuleSet = true;
            this.OnPropertyChanged(new(nameof(CanAddRuleSet)));
        }
    }


    /// <inheritdoc/>
    protected override Task<OperationDurationAnalysisRuleSet> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileName, false);
    

    /// <inheritdoc/>
    protected override void OnProductActivationChanged(string productId, bool isActivated)
    {
        base.OnProductActivationChanged(productId, isActivated);
        if (productId != Products.Professional)
            return;
        if (isActivated)
        {
            this.ScheduleSavingProfiles();
            this.LoadProfilesAsync();
            if (!this.CanAddRuleSet)
            {
                this.CanAddRuleSet = true;
                this.OnPropertyChanged(new(nameof(CanAddRuleSet)));
            }
        }
        else
        {
            foreach (var ruleSet in this.Profiles.ToArray())
                this.RemoveProfile(ruleSet, false);
            this.CancelSavingProfiles();
            if (!this.CanAddRuleSet)
            {
                this.CanAddRuleSet = true;
                this.OnPropertyChanged(new(nameof(CanAddRuleSet)));
            }
        }
    }


    /// <inheritdoc/>
    protected override Task OnSaveProfileAsync(OperationDurationAnalysisRuleSet profile, string fileName)
    {
        if (this.Application.ProductManager.IsProductActivated(Products.Professional))
            return base.OnSaveProfileAsync(profile, fileName);
        this.Logger.LogWarning($"Skip saving profile '{profile.Name}' ({profile.Id})");
        return Task.CompletedTask;
    }


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "OperationDurationAnalysisRules");


    /// <summary>
    /// Remove given rule set.
    /// </summary>
    /// <param name="rule">Rule set to remove.</param>
    /// <returns>True if rule set has been removed successfully.</returns>
    public bool RemoveRuleSet(OperationDurationAnalysisRuleSet rule)
    {
        var result = this.RemoveProfile(rule);
        if (result
            && !this.Application.ProductManager.IsProductActivated(Products.Professional)
            && this.Profiles.Count == 0
            && !this.CanAddRuleSet)
        {
            this.CanAddRuleSet = true;
            this.OnPropertyChanged(new(nameof(CanAddRuleSet)));
        }
        return result;
    }


    /// <summary>
    /// Get all rule sets.
    /// </summary>
    public IReadOnlyList<OperationDurationAnalysisRuleSet> RuleSets { get => this.Profiles; }
}