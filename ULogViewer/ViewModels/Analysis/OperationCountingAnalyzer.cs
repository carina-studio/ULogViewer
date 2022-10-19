using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analyzer to count operations with specific intervals.
/// </summary>
class OperationCountingAnalyzer : RuleBasedDisplayableLogAnalyzer<OperationCountingAnalyzer.AnalyzingToken, OperationCountingAnalysisRuleSet.Rule>
{
    /// <summary>
    /// Token of analyzing.
    /// </summary>
    public class AnalyzingToken
    {
        public readonly DisplayableLogAnalysisContext Context = new();
        public readonly IDictionary<string, Func<DisplayableLog, object?>> LogPropertyGetters = new Dictionary<string, Func<DisplayableLog, object?>>();
        public readonly IList<Func<DisplayableLog, string?>> LogTextPropertyGetters = new List<Func<DisplayableLog, string?>>();
        public readonly ISet<OperationCountingAnalysisRuleSet.Rule> Rules = new HashSet<OperationCountingAnalysisRuleSet.Rule>();
    }


    // Result.
    class Result : DisplayableLogAnalysisResult
    {
        // Fields.
        readonly string operationName;

        // Constructor.
        public Result(OperationCountingAnalyzer analyzer, DisplayableLogAnalysisResultType type, DisplayableLog? beginningLog, DisplayableLog? endingLog, string operationName, TimeSpan duration, int quantity) : base(analyzer, type, null)
        {
            this.BeginningLog = beginningLog;
            this.Duration = duration;
            this.EndingLog = endingLog;
            this.operationName = operationName;
            this.Quantity = quantity;
        }

        /// <inheritdoc/>
        public override DisplayableLog? BeginningLog { get; }

        /// <inheritdoc/>
        public override TimeSpan? Duration { get; }

        /// <inheritdoc/>
        public override DisplayableLog? EndingLog { get; }

        /// <inheritdoc/>
        public override long? Quantity { get; }

        // Update message.
        protected override string? OnUpdateMessage() =>
            this.operationName;
    }


    // Fields.
    readonly ObservableList<DisplayableLogProperty> logProperties = new();


    /// <summary>
    /// Initialize new <see cref="OperationCountingAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source logs.</param>
    /// <param name="comparison">Comparison for source logs.</param>
    public OperationCountingAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
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
    void OnLogPropertiesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        this.InvalidateProcessing();


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalyzingToken token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        result = EmptyResults;
        return false;
    }
}