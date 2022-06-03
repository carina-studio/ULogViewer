using CarinaStudio.Collections;
using CarinaStudio.Data.Converters;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Analyzer to analyze operation duration from logs.
/// </summary>
class OperationDurationDisplayableLogAnalyzer : ContextualBasedDisplayableLogAnalyzer<OperationDurationDisplayableLogAnalyzer.AnalysisContext>
{
    /// <summary>
    /// Context of analysis.
    /// </summary>
    public class AnalysisContext : ContextualBasedAnalysisContext
    {
        public Func<DisplayableLog, TimeSpan?>? BeginningTimeSpanGetter;
        public Func<DisplayableLog, DateTime?>? BeginningTimestampGetter;
        public Func<DisplayableLog, TimeSpan?>? EndingTimeSpanGetter;
        public Func<DisplayableLog, DateTime?>? EndingTimestampGetter;
        public OperationDurationAnalysisRuleSet[] RuleSets = new OperationDurationAnalysisRuleSet[0];
        public Func<DisplayableLog, string?>[] TextPropertyGetters = new Func<DisplayableLog, string?>[0];
    }


    // Result of analysis.
    class Result : DisplayableLogAnalysisResult
    {
        // Fields.
        readonly TimeSpan duration;
        readonly string operationName;

        // Constructor.
        public Result(OperationDurationDisplayableLogAnalyzer analyzer, string operationName, DisplayableLog beginningLog, TimeSpan duration) : base(analyzer, DisplayableLogAnalysisResultType.TimeSpan, beginningLog)
        {
            this.duration = duration;
            this.operationName = operationName;
        }

        // Memory size.
        public override long MemorySize => base.MemorySize + 8 + IntPtr.Size + (this.operationName.Length << 1);

        // Update message.
        protected override string? OnUpdateMessage() =>
            $"{this.operationName}\n{AppSuite.Converters.TimeSpanConverter.Default.Convert<string>(this.duration)}";
    }


    // Static fields.
    [ThreadStatic]
    static StringBuilder? LogTextBuffer;


    // Fields.
    readonly List<OperationDurationAnalysisRuleSet> attachedRuleSets = new();
    readonly ObservableList<DisplayableLogProperty> logProperties = new(); 

    readonly ObservableList<OperationDurationAnalysisRuleSet> ruleSets = new();


    /// <summary>
    /// Initialize new <see cref="OperationDurationDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    protected OperationDurationDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.logProperties.CollectionChanged += (_, e) => this.InvalidateProcessing();
        this.ruleSets.CollectionChanged += this.OnRuleSetsChanged;
    }


    /// <inheritdoc/>
    protected override AnalysisContext CreateProcessingToken(out bool isProcessingNeeded)
    {
        // check state
        isProcessingNeeded = false;
        var context = new AnalysisContext();
        if (this.logProperties.IsEmpty() || this.ruleSets.IsEmpty())
            return context;
        
        // complete
        isProcessingNeeded = true;
        return context;
    }


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalysisContext token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        result = this.EmptyResults;
        return false;
    }


    // Called when rule set changed.
    void OnRuleSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationDurationAnalysisRuleSet.Rules))
            this.InvalidateProcessing();
    }


    // Called when list of rule sets changed.
    void OnRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<OperationDurationAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<OperationDurationAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                break;
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<OperationDurationAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                e.NewItems!.Cast<OperationDurationAnalysisRuleSet>().Let(it =>
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
    public IList<OperationDurationAnalysisRuleSet> RuleSets { get => this.ruleSets; }
}