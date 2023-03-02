using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Analyzer to analyze operation duration from logs.
/// </summary>
class OperationDurationDisplayableLogAnalyzer : RuleBasedDisplayableLogAnalyzer<OperationDurationDisplayableLogAnalyzer.AnalysisContext, OperationDurationAnalysisRuleSet.Rule>
{
    /// <summary>
    /// Context of analysis.
    /// </summary>
    public class AnalysisContext : ContextualBasedAnalysisContext
    {
        public Func<DisplayableLog, TimeSpan?>? BeginningTimeSpanGetter;
        public Func<DisplayableLog, DateTime?>? BeginningTimestampGetter;
        public readonly Dictionary<OperationDurationAnalysisRuleSet.Rule, StringFormatter> CustomMessageFormatters = new();
        public Func<DisplayableLog, TimeSpan?>? EndingTimeSpanGetter;
        public Func<DisplayableLog, DateTime?>? EndingTimestampGetter;
        public OperationDurationAnalysisRuleSet[] RuleSets = Array.Empty<OperationDurationAnalysisRuleSet>();
        public Func<DisplayableLog, string?>[] TextPropertyGetters = Array.Empty<Func<DisplayableLog, string?>>();
    }


    // Result of analysis.
    public class Result : DisplayableLogAnalysisResult
    {
        // Fields.
        readonly string? customMessage;
        readonly string operationName;

        // Constructor.
        public Result(OperationDurationDisplayableLogAnalyzer analyzer, string operationName, DisplayableLogAnalysisResultType resultType, DisplayableLog beginningLog, DisplayableLog endingLog, TimeSpan duration, string? customMessage, long? byteSize, long? quantity) : base(analyzer, resultType, beginningLog)
        {
            this.customMessage = customMessage;
            this.Duration = duration;
            this.operationName = operationName;
            this.Quantity = quantity;
        }

        // Beginning log.
        public override DisplayableLog? BeginningLog { get; }

        /// <inheritdoc/>
        public override long? ByteSize { get; }

        // Duration.
        public override TimeSpan? Duration { get; }

        // Beginning log.
        public override DisplayableLog? EndingLog { get; }

        // Memory size.
        public override long MemorySize => base.MemorySize 
            + (2 * IntPtr.Size); // customMessage, operationName

        // Update message.
        protected override string? OnUpdateMessage()
        {
            if (string.IsNullOrWhiteSpace(this.customMessage))
                return this.operationName;
            return this.customMessage;
        }

        /// <inheritdoc/>
        public override long? Quantity { get; }
    }


    // Static fields.
    //


    // Fields.
    readonly ObservableList<DisplayableLogProperty> logProperties = new();


    /// <summary>
    /// Initialize new <see cref="OperationDurationDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public OperationDurationDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.logProperties.CollectionChanged += (_, e) => this.InvalidateProcessing();
    }


    /// <inheritdoc/>
    protected override AnalysisContext CreateProcessingToken(out bool isProcessingNeeded)
    {
        // check state
        isProcessingNeeded = false;
        var context = new AnalysisContext();
        if (this.logProperties.IsEmpty() || this.RuleSets.IsEmpty())
            return context;
        
        // complete
        isProcessingNeeded = true;
        return context;
    }


    /// <summary>
    /// Get list of log properties to be included in analysis.
    /// </summary>
    public IList<DisplayableLogProperty> LogProperties { get => this.logProperties; }


    /// <inheritdoc/>
    protected override int MaxConcurrencyLevel => 1;


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalysisContext token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        result = this.EmptyResults;
        return false;
    }
}