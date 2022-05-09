using CarinaStudio.AppSuite.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Default instance.
    /// </summary>
    public static KeyLogAnalysisRuleManager Default { get => defaultInstance ?? throw new InvalidOperationException(); }


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
        
        // create instance
        defaultInstance = new(app);
    }


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "KeyLogAnalysis");


    /// <summary>
    /// Get all rules.
    /// </summary>
    public IReadOnlyList<KeyLogAnalysisRule> Rules { get => base.Profiles; }
}