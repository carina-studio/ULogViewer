using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Manager of <see cref="KeyLogAnalysisRuleSet"/>.
/// </summary>
class KeyLogAnalysisRuleSetManager : BaseProfileManager<IULogViewerApplication, KeyLogAnalysisRuleSet>
{
    // Static fields.
    static KeyLogAnalysisRuleSetManager? defaultInstance;


    // Constructor.
    KeyLogAnalysisRuleSetManager(IULogViewerApplication app) : base(app)
    { }


    /// <summary>
    /// Add rule set.
    /// </summary>
    /// <param name="rule">Rule to add.</param>
    public void AddRuleSet(KeyLogAnalysisRuleSet rule)
    {
        this.VerifyAccess();
        if (rule.Manager != null)
            throw new InvalidOperationException();
        if (this.GetProfileOrDefault(rule.Id) != null)
            rule.ChangeId();
        this.AddProfile(rule);
    }


    /// <summary>
    /// Default instance.
    /// </summary>
    public static KeyLogAnalysisRuleSetManager Default { get => defaultInstance ?? throw new InvalidOperationException(); }


    /// <summary>
    /// Get rule set with given ID.
    /// </summary>
    /// <param name="id">ID of rule set.</param>
    /// <returns>Rule set with given ID or Null if rule cannot be found.</returns>
    public KeyLogAnalysisRuleSet? GetRuleSetOrDefault(string id) =>
        this.GetProfileOrDefault(id);


    /// <summary>
    /// Initialize asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static async Task InitializeAsync(IULogViewerApplication app)
    {
        // check state
        if (defaultInstance != null)
            throw new InvalidOperationException();
        
        // initialize
        defaultInstance = new(app);
        await defaultInstance.WaitForInitialization();
    }


    /// <inheritdoc/>
    protected override Task<KeyLogAnalysisRuleSet> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        KeyLogAnalysisRuleSet.LoadAsync(this.Application, fileName);


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "KeyLogAnalysisRules");


    /// <summary>
    /// Remove given rule set.
    /// </summary>
    /// <param name="rule">Rule set to remove.</param>
    /// <returns>True if rule set has been removed successfully.</returns>
    public bool RemoveRuleSet(KeyLogAnalysisRuleSet rule) =>
        this.RemoveProfile(rule);


    /// <summary>
    /// Get all rule sets.
    /// </summary>
    public IReadOnlyList<KeyLogAnalysisRuleSet> RuleSets { get => base.Profiles; }
}