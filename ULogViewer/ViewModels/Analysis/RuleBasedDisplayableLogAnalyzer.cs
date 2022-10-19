using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base class for log analyzer which analyzes logs base-on rules.
/// </summary>
abstract class RuleBasedDisplayableLogAnalyzer<TAnalysisToken, TRule> : BaseDisplayableLogAnalyzer<TAnalysisToken> where TAnalysisToken : class where TRule : class, IEquatable<TRule>
{
    // Fields.
    readonly List<DisplayableLogAnalysisRuleSet<TRule>> attachedRuleSets = new();
    readonly ObservableList<DisplayableLogAnalysisRuleSet<TRule>> ruleSets = new();


    /// <summary>
    /// Initialize new <see cref="RuleBasedDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source logs.</param>
    /// <param name="comparison">Comparison for source logs.</param>
    protected RuleBasedDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.ruleSets.CollectionChanged += (_, e) => this.OnRuleSetsChanged(e);
    }


    /// <summary>
    /// Collect all rules from rule sets.
    /// </summary>
    /// <returns>Rules.</returns>
    protected ISet<TRule> CollectRules()
    {
        var rules = new HashSet<TRule>();
        foreach (var ruleSet in this.ruleSets)
            rules.AddAll(ruleSet.Rules);
        return rules;
    }


    /// <summary>
    /// Collect all rules from rule sets.
    /// </summary>
    /// <param name="rules">Set to receive collected rules.</param>
    protected void CollectRules(ISet<TRule> rules)
    {
        rules.Clear();
        foreach (var ruleSet in this.ruleSets)
            rules.AddAll(ruleSet.Rules);
    }


    // Called when rule set changed.
    void OnRuleSetPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        this.OnRuleSetPropertyChanged((DisplayableLogAnalysisRuleSet<TRule>)sender.AsNonNull(), e);


    /// <summary>
    /// Called when property of rule set changed.
    /// </summary>
    /// <param name="ruleSet">Rule set.</param>
    /// <param name="e">Event data.</param>
    protected virtual void OnRuleSetPropertyChanged(DisplayableLogAnalysisRuleSet<TRule> ruleSet, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyLogAnalysisRuleSet.Rules))
            this.InvalidateProcessing();
    }


    /// <summary>
    /// Called when list of rule sets changed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnRuleSetsChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<DisplayableLogAnalysisRuleSet<TRule>>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogAnalysisRuleSet<TRule>>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                break;
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<DisplayableLogAnalysisRuleSet<TRule>>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                e.NewItems!.Cast<DisplayableLogAnalysisRuleSet<TRule>>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var ruleSet in this.attachedRuleSets)
                    ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                this.attachedRuleSets.Clear();
                foreach (var ruleSet in this.ruleSets)
                    ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                this.attachedRuleSets.AddRange(this.ruleSets);
                break;
            default:
                throw new NotSupportedException("Unsupported change of rule sets.");
        }
        this.InvalidateProcessing();
    }


    /// <summary>
    /// Get list of rule sets for analysis.
    /// </summary>
    public IList<DisplayableLogAnalysisRuleSet<TRule>> RuleSets { get => this.ruleSets; }
}