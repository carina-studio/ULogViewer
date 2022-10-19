using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analyzer to find key logs.
/// </summary>
class KeyLogDisplayableLogAnalyzer : RuleBasedDisplayableLogAnalyzer<KeyLogDisplayableLogAnalyzer.AnalyzingToken, KeyLogAnalysisRuleSet.Rule>
{
    /// <summary>
    /// Token of analyzing.
    /// </summary>
    public class AnalyzingToken
    { }


    // Fields.
    readonly ObservableList<DisplayableLogProperty> logProperties = new();


    /// <summary>
    /// Initialize new <see cref="KeyLogDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source logs.</param>
    /// <param name="comparison">Comparison for source logs.</param>
    public KeyLogDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.logProperties.CollectionChanged += this.OnLogPropertiesChanged;
    }


    /// <inheritdoc/>
    protected override AnalyzingToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        isProcessingNeeded = false;
        return new AnalyzingToken();
    }


    /// <summary>
    /// Get list of log properties to be included in analysis.
    /// </summary>
    public IList<DisplayableLogProperty> LogProperties { get => this.logProperties; }


    // Called whe list of log properties changed.
    void OnLogPropertiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    { }


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalyzingToken token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        result = this.EmptyResults;
        return false;
    }
}