using System.Collections.Generic;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using System;
using System.IO;
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
        if (this.GetProfileOrDefault(rule.Id) != null)
            rule.ChangeId();
        this.AddProfile(rule);
    }


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
    protected override Task<OperationDurationAnalysisRuleSet> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileName);


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "OperationDurationAnalysisRules");


    /// <summary>
    /// Remove given rule set.
    /// </summary>
    /// <param name="rule">Rule set to remove.</param>
    /// <returns>True if rule set has been removed successfully.</returns>
    public bool RemoveRuleSet(OperationDurationAnalysisRuleSet rule) =>
        this.RemoveProfile(rule);


    /// <summary>
    /// Get all rule sets.
    /// </summary>
    public IReadOnlyList<OperationDurationAnalysisRuleSet> RuleSets { get => this.Profiles; }
}