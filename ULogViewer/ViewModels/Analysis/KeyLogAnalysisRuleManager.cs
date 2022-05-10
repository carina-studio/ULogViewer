using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Manager of <see cref="KeyLogAnalysisRule"/>.
/// </summary>
class KeyLogAnalysisRuleManager : BaseProfileManager<IULogViewerApplication, KeyLogAnalysisRule>
{
    // Static fields.
    static KeyLogAnalysisRuleManager? defaultInstance;


    // Constructor.
    KeyLogAnalysisRuleManager(IULogViewerApplication app) : base(app)
    { }


    /// <summary>
    /// Add rule.
    /// </summary>
    /// <param name="rule">Rule to add.</param>
    public void AddRule(KeyLogAnalysisRule rule)
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
    public static KeyLogAnalysisRuleManager Default { get => defaultInstance ?? throw new InvalidOperationException(); }


    /// <summary>
    /// Get rule with given ID.
    /// </summary>
    /// <param name="id">ID of rule.</param>
    /// <returns>Rule with given ID or Null if rule cannot be found.</returns>
    public KeyLogAnalysisRule? GetRuleOrDefault(string id) =>
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
    protected override Task<KeyLogAnalysisRule> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "KeyLogAnalysis");


    /// <summary>
    /// Remove given rule.
    /// </summary>
    /// <param name="rule">Rule to remove.</param>
    /// <returns>True if rule has been removed successfully.</returns>
    public bool RemoveRule(KeyLogAnalysisRule rule) =>
        this.RemoveProfile(rule);


    /// <summary>
    /// Get all rules.
    /// </summary>
    public IReadOnlyList<KeyLogAnalysisRule> Rules { get => base.Profiles; }
}