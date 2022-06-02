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


    /// <inheritdoc/>
    protected override Task<OperationDurationAnalysisRuleSet> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileName);


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "OperationDurationAnalysisRules");
}