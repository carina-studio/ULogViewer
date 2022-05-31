using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analyzer to find key logs.
/// </summary>
class KeyLogDisplayableLogAnalyzer : BaseDisplayableLogAnalyzer<KeyLogDisplayableLogAnalyzer.AnalyzingToken>
{
    /// <summary>
    /// Token of analyzing.
    /// </summary>
    public class AnalyzingToken
    { }


    // Fields.
    readonly List<KeyLogAnalysisRuleSet> attachedRuleSets = new();
    readonly ObservableList<DisplayableLogProperty> logProperties = new(); 
    readonly ObservableList<KeyLogAnalysisRuleSet> ruleSets = new();


    /// <summary>
    /// Initialize new <see cref="KeyLogDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source logs.</param>
    /// <param name="comparison">Comparison for source logs.</param>
    public KeyLogDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.logProperties.CollectionChanged += this.OnLogPropertiesChanged;
        this.ruleSets.CollectionChanged += this.OnRuleSetsChanged;
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


    // Called when rule set changed.
    void OnRuleSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    { }


    // Called when list of rule sets changed.
    void OnRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    { }


    /// <summary>
    /// Get list of rule sets for analysis.
    /// </summary>
    public IList<KeyLogAnalysisRuleSet> RuleSets { get => this.ruleSets; }
}