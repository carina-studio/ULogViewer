using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

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
    {
        public readonly ISet<KeyLogAnalysisRuleSet.Rule> Rules = new HashSet<KeyLogAnalysisRuleSet.Rule>();
    }


    // Result.
    class Result : DisplayableLogAnalysisResult
    {
        // Fields.
        readonly string message;

        // Constructor.
        public Result(KeyLogDisplayableLogAnalyzer analyzer, DisplayableLogAnalysisResultType type, DisplayableLog log, string message) : base(analyzer, type, log) =>
            this.message = message;

        // Update message.
        protected override string? OnUpdateMessage() =>
            this.message;
    }


    // Fields.
    readonly List<KeyLogAnalysisRuleSet> attachedRuleSets = new();
    readonly ObservableList<KeyLogAnalysisRuleSet> ruleSets = new();


    /// <summary>
    /// Initialize new <see cref="KeyLogDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source logs.</param>
    /// <param name="comparison">Comparison for source logs.</param>
    public KeyLogDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { }


    /// <inheritdoc/>
    protected override AnalyzingToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        // collect rules
        isProcessingNeeded = false;
        var token = new AnalyzingToken();
        if (this.ruleSets.IsEmpty())
            return token;
        foreach (var ruleSet in this.ruleSets)
            token.Rules.AddAll(ruleSet.Rules);
        if (token.Rules.IsEmpty())
            return token;

        // complete
        isProcessingNeeded = true;
        return token;
    }


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalyzingToken token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        // get text to match
        var text = $"{log.SourceName}$${log.Message}";
        if (text == null)
        {
            result = this.EmptyResults;
            return false;
        }

        // match rules
        var partialResults = (List<DisplayableLogAnalysisResult>?)null;
        foreach (var rule in token.Rules)
        {
            var match = rule.Pattern.Match(text);
            if (match.Success)
            {
                // generate message
                var message = rule.FormattedMessage;

                // generate result
                partialResults ??= new();
                partialResults.Add(new Result(this, rule.ResultType, log, message));
            }
        }

        // complete
        if (partialResults == null)
        {
            result = this.EmptyResults;
            return false;
        }
        result = partialResults;
        return true;
    }


    // Called when rule set changed.
    void OnRuleSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyLogAnalysisRuleSet.Rules))
            this.InvalidateProcessing();
    }


    // Called when list of rule sets changed.
    void OnRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                break;
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                e.NewItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
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
    public IList<KeyLogAnalysisRuleSet> RuleSets { get => this.ruleSets; }
}