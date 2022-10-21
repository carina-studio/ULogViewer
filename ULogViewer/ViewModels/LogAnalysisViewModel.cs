using System;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using CarinaStudio.ULogViewer.Logs.Profiles;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log analysis.
/// </summary>
class LogAnalysisViewModel : SessionComponent
{
    /// <summary>
    /// Property of <see cref="AnalysisProgress"/>.
    /// </summary>
    public static readonly ObservableProperty<double> AnalysisProgressProperty = ObservableProperty.Register<LogAnalysisViewModel, double>(nameof(AnalysisProgress));
    /// <summary>
    /// Property of <see cref="HasSelectedAnalysisResults"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> HasSelectedAnalysisResultsProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(HasSelectedAnalysisResults));
    /// <summary>
    /// Property of <see cref="IsAnalyzing"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsAnalyzingProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(IsAnalyzing));
    /// <summary>
    /// Property of <see cref="IsPanelVisible"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsPanelVisibleProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(IsPanelVisible), false);
    /// <summary>
    /// Property of <see cref="PanelSize"/>.
    /// </summary>
    public static readonly ObservableProperty<double> PanelSizeProperty = ObservableProperty.Register<LogAnalysisViewModel, double>(nameof(PanelSize), (Session.MinSidePanelSize + Session.MaxSidePanelSize) / 2, 
        coerce: (_, it) =>
        {
            if (it >= Session.MaxSidePanelSize)
                return Session.MaxSidePanelSize;
            if (it < Session.MinSidePanelSize)
                return Session.MinSidePanelSize;
            return it;
        }, 
        validate: it => double.IsFinite(it));
    /// <summary>
    /// Property of <see cref="SelectedAnalysisResultsAverageByteSize"/>.
    /// </summary>
    public static readonly ObservableProperty<long?> SelectedAnalysisResultsAverageByteSizeProperty = ObservableProperty.Register<LogAnalysisViewModel, long?>(nameof(SelectedAnalysisResultsAverageByteSize));
    /// <summary>
    /// Property of <see cref="SelectedAnalysisResultsAverageDuration"/>.
    /// </summary>
    public static readonly ObservableProperty<TimeSpan?> SelectedAnalysisResultsAverageDurationProperty = ObservableProperty.Register<LogAnalysisViewModel, TimeSpan?>(nameof(SelectedAnalysisResultsAverageDuration));
    /// <summary>
    /// Property of <see cref="SelectedAnalysisResultsAverageQuantity"/>.
    /// </summary>
    public static readonly ObservableProperty<double?> SelectedAnalysisResultsAverageQuantityProperty = ObservableProperty.Register<LogAnalysisViewModel, double?>(nameof(SelectedAnalysisResultsAverageQuantity));
    /// <summary>
    /// Property of <see cref="SelectedAnalysisResultsTotalByteSize"/>.
    /// </summary>
    public static readonly ObservableProperty<long?> SelectedAnalysisResultsTotalByteSizeProperty = ObservableProperty.Register<LogAnalysisViewModel, long?>(nameof(SelectedAnalysisResultsTotalByteSize));
    /// <summary>
    /// Property of <see cref="SelectedAnalysisResultsTotalDuration"/>.
    /// </summary>
    public static readonly ObservableProperty<TimeSpan?> SelectedAnalysisResultsTotalDurationProperty = ObservableProperty.Register<LogAnalysisViewModel, TimeSpan?>(nameof(SelectedAnalysisResultsTotalDuration));
    /// <summary>
    /// Property of <see cref="SelectedAnalysisResultsTotalQuantity"/>.
    /// </summary>
    public static readonly ObservableProperty<long?> SelectedAnalysisResultsTotalQuantityProperty = ObservableProperty.Register<LogAnalysisViewModel, long?>(nameof(SelectedAnalysisResultsTotalQuantity));


    // Constants.
    const int LogsAnalysisStateUpdateDelay = 300;


    // Static fields.
    static readonly SettingKey<double> latestPanelSizeKey = new SettingKey<double>("Session.LatestLogAnalysisPanelSize", PanelSizeProperty.DefaultValue);
    [Obsolete]
	static readonly SettingKey<double> latestSidePanelSizeKey = new SettingKey<double>("Session.LatestSidePanelSize", Session.MarkedLogsPanelSizeProperty.DefaultValue);

    
    // Fields.
    readonly DisplayableLog?[] analysisResultComparisonTempLogs1 = new DisplayableLog?[3];
    readonly DisplayableLog?[] analysisResultComparisonTempLogs2 = new DisplayableLog?[3];
    readonly SortedObservableList<DisplayableLogAnalysisResult> analysisResults;
    readonly HashSet<IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>> attachedAnalyzers = new();
    readonly IDisposable displayLogPropertiesObserverToken;
    bool isRestoringState;
    readonly ObservableList<KeyLogAnalysisRuleSet> keyLogAnalysisRuleSets = new();
    readonly KeyLogDisplayableLogAnalyzer keyLogAnalyzer;
	readonly ObservableList<LogAnalysisScriptSet> logAnalysisScriptSets = new();
    readonly ObservableList<OperationCountingAnalysisRuleSet> operationCountingAnalysisRuleSets = new();
    readonly OperationCountingAnalyzer operationCountingAnalyzer;
    readonly ObservableList<OperationDurationAnalysisRuleSet> operationDurationAnalysisRuleSets = new();
	readonly OperationDurationDisplayableLogAnalyzer operationDurationAnalyzer;
    readonly ScheduledAction reportSelectedAnalysisResultsInfoAction;
    readonly ScriptDisplayableLogAnalyzer scriptLogAnalyzer;
    readonly SortedObservableList<DisplayableLogAnalysisResult> selectedAnalysisResults;
    readonly ScheduledAction updateAnalysisStateAction;
    readonly ScheduledAction updateKeyLogAnalysisAction;
    readonly ScheduledAction updateOperationCountingAnalysisAction;
    readonly ScheduledAction updateOperationDurationAnalysisAction;
    readonly ScheduledAction updateScriptLogAnalysis;
    

    /// <summary>
    /// Initialize new <see cref="LogAnalysisViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    public LogAnalysisViewModel(Session session) : base(session)
    { 
        // start initialization
        bool isInit = true;

        // create commands
        this.ClearSelectedAnalysisResultsCommand = new Command(() => this.selectedAnalysisResults?.Clear(), this.GetValueAsObservable(HasSelectedAnalysisResultsProperty));
        this.CopySelectedAnalysisResultsCommand = new Command(this.CopySelectedAnalysisResults, this.GetValueAsObservable(HasSelectedAnalysisResultsProperty));

        // create collections
        this.analysisResults = new SortedObservableList<DisplayableLogAnalysisResult>(this.CompareAnalysisResults).Also(it =>
            it.CollectionChanged += this.OnAnalysisResultsChanged);
        this.selectedAnalysisResults = new SortedObservableList<DisplayableLogAnalysisResult>(this.CompareAnalysisResults).Also(it =>
            it.CollectionChanged += this.OnSelectedAnalysisResultsChanged);

        // create analyzers
        this.keyLogAnalysisRuleSets.CollectionChanged += (_, e) => 
            this.updateKeyLogAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.keyLogAnalyzer = new KeyLogDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        this.logAnalysisScriptSets.CollectionChanged += (_, e) =>
            this.updateScriptLogAnalysis?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.operationCountingAnalysisRuleSets.CollectionChanged += (_, e) => 
            this.updateOperationCountingAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.operationCountingAnalyzer = new OperationCountingAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        this.operationDurationAnalysisRuleSets.CollectionChanged += (_, e) => 
            this.updateOperationDurationAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.operationDurationAnalyzer = new OperationDurationDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        this.scriptLogAnalyzer = new ScriptDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        
        // setup properties
        this.AnalysisResults = this.analysisResults.AsReadOnly();

        // create scheduled actions
        this.reportSelectedAnalysisResultsInfoAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            var byteSizeCount = 0;
            var totalByteSize = 0L;
            var durationResultCount = 0;
            var totalDuration = new TimeSpan();
            var quantityCount = 0;
            var totalQuantity = 0L;
            for (var i = this.selectedAnalysisResults.Count - 1; i >= 0; --i)
            {
                var result = this.selectedAnalysisResults[i];
                result.ByteSize?.Let(it =>
                {
                    ++byteSizeCount;
                    totalByteSize += it;
                });
                result.Duration?.Let(it =>
                {
                    ++durationResultCount;
                    totalDuration += it;
                });
                result.Quantity?.Let(it =>
                {
                    ++quantityCount;
                    totalQuantity += it;
                });
            }
            if (byteSizeCount == 0)
            {
                this.SetValue(SelectedAnalysisResultsAverageByteSizeProperty, null);
                this.SetValue(SelectedAnalysisResultsTotalByteSizeProperty, null);
            }
            else
            {
                this.SetValue(SelectedAnalysisResultsAverageByteSizeProperty, byteSizeCount > 1 ? totalByteSize / byteSizeCount : null);
                this.SetValue(SelectedAnalysisResultsTotalByteSizeProperty, totalByteSize);
            }
            if (durationResultCount == 0)
            {
                this.SetValue(SelectedAnalysisResultsAverageDurationProperty, null);
                this.SetValue(SelectedAnalysisResultsTotalDurationProperty, null);
            }
            else
            {
                this.SetValue(SelectedAnalysisResultsAverageDurationProperty, durationResultCount > 1 ? totalDuration / durationResultCount : null);
                this.SetValue(SelectedAnalysisResultsTotalDurationProperty, totalDuration);
            }
            if (quantityCount == 0)
            {
                this.SetValue(SelectedAnalysisResultsAverageQuantityProperty, null);
                this.SetValue(SelectedAnalysisResultsTotalQuantityProperty, null);
            }
            else
            {
                this.SetValue(SelectedAnalysisResultsAverageQuantityProperty, quantityCount > 1 ? totalQuantity / (double)quantityCount : null);
                this.SetValue(SelectedAnalysisResultsTotalQuantityProperty, totalQuantity);
            }
        });
        this.updateKeyLogAnalysisAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.keyLogAnalyzer.RuleSets.SequenceEqual(this.keyLogAnalysisRuleSets))
            {
                this.keyLogAnalyzer.RuleSets.Clear();
                this.keyLogAnalyzer.RuleSets.AddAll(this.keyLogAnalysisRuleSets);
            }
        });
        this.updateAnalysisStateAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            var isAnalyzing = false;
            var progress = 1.0;
            foreach (var analyzer in this.attachedAnalyzers)
            {
                if (analyzer.IsProcessing)
                {
                    isAnalyzing = true;
                    progress = Math.Min(progress, analyzer.Progress);
                }
            }
            if (isAnalyzing)
            {
                this.SetValue(IsAnalyzingProperty, true);
                this.SetValue(AnalysisProgressProperty, progress);
            }
            else
            {
                this.SetValue(IsAnalyzingProperty, false);
                this.SetValue(AnalysisProgressProperty, 0);
            }
        });
        this.updateOperationCountingAnalysisAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.operationCountingAnalyzer.RuleSets.SequenceEqual(this.operationCountingAnalysisRuleSets))
            {
                this.operationCountingAnalyzer.RuleSets.Clear();
                this.operationCountingAnalyzer.RuleSets.AddAll(this.operationCountingAnalysisRuleSets);
            }
        });
        this.updateOperationDurationAnalysisAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.operationDurationAnalyzer.RuleSets.SequenceEqual(this.operationDurationAnalysisRuleSets))
            {
                this.operationDurationAnalyzer.RuleSets.Clear();
                this.operationDurationAnalyzer.RuleSets.AddAll(this.operationDurationAnalysisRuleSets);
            }
        });
        this.updateScriptLogAnalysis = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.scriptLogAnalyzer.ScriptSets.SequenceEqual(this.logAnalysisScriptSets))
            {
                this.scriptLogAnalyzer.ScriptSets.Clear();
                this.scriptLogAnalyzer.ScriptSets.AddAll(this.logAnalysisScriptSets);
            }
        });

        // attach to self properties
        this.GetValueAsObservable(PanelSizeProperty).Subscribe(size =>
        {
            if (!isInit && !this.isRestoringState)
                this.PersistentState.SetValue<double>(latestPanelSizeKey, size);
        });
        
        // attach to session
        session.AllLogReadersDisposed += this.OnAllLogReadersDisposed;
        this.displayLogPropertiesObserverToken = session.GetValueAsObservable(Session.DisplayLogPropertiesProperty).Subscribe(properties =>
        {
            this.keyLogAnalyzer.LogProperties.Clear();
            this.keyLogAnalyzer.LogProperties.AddAll(properties);
            this.operationCountingAnalyzer.LogProperties.Clear();
            this.operationCountingAnalyzer.LogProperties.AddAll(properties);
            this.operationDurationAnalyzer.LogProperties.Clear();
            this.operationDurationAnalyzer.LogProperties.AddAll(properties);
            this.scriptLogAnalyzer.LogProperties.Clear();
            this.scriptLogAnalyzer.LogProperties.AddAll(properties);
        });

        // restore state
#pragma warning disable CS0612
        if (this.PersistentState.GetRawValue(latestSidePanelSizeKey) is double sidePanelSize)
            this.SetValue(PanelSizeProperty, sidePanelSize);
        else
            this.SetValue(PanelSizeProperty, this.PersistentState.GetValueOrDefault(latestPanelSizeKey));
#pragma warning restore CS0612

        // complete initialization
        isInit = false;
    }


    /// <summary>
    /// Get progress of logs analysis.
    /// </summary>
    public double AnalysisProgress { get => this.GetValue(AnalysisProgressProperty); }


    /// <summary>
    /// Get list of result of log analysis.
    /// </summary>
    public IList<DisplayableLogAnalysisResult> AnalysisResults { get; }


    // Attach to given log analyzer.
    void AttachToAnalyzer(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
    {
        // add to set
        if (!this.attachedAnalyzers.Add(analyzer))
            return;
        
        // add handlers
        if (analyzer.AnalysisResults is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged += this.OnAnalyzerAnalysisResultsChanged;
        analyzer.ErrorMessageGenerated += this.OnAnalyzerErrorMessageGenerated;
        analyzer.PropertyChanged += this.OnAnalyzerPropertyChanged;

        // add analysis results
        this.analysisResults.AddAll(analyzer.AnalysisResults);

        // report state
        this.updateAnalysisStateAction?.Schedule();
    }


    /// <summary>
    /// Command to clear selected analysis results.
    /// </summary>
    public ICommand ClearSelectedAnalysisResultsCommand { get; }


    // Compare log analysis results.
    int CompareAnalysisResults(DisplayableLogAnalysisResult? lhs, DisplayableLogAnalysisResult? rhs)
    {
        // get logs
        var lhsLogs = this.analysisResultComparisonTempLogs1;
        lhsLogs[0] = lhs!.BeginningLog;
        lhsLogs[1] = lhs.Log;
        lhsLogs[2] = lhs.EndingLog;
        var rhsLogs = this.analysisResultComparisonTempLogs2;
        rhsLogs[0] = rhs!.BeginningLog;
        rhsLogs[1] = rhs.Log;
        rhsLogs[2] = rhs.EndingLog;

        // compare logs
        var logCount = lhsLogs.Length;
        var lhsLogIndex = 0;
        var rhsLogIndex = 0;
        var result = 0;
        while (lhsLogIndex < logCount && rhsLogIndex < logCount)
        {
            var lhsLog = lhsLogs[lhsLogIndex];
            var rhsLog = rhsLogs[rhsLogIndex];
            if (lhsLog == null)
                ++lhsLogIndex;
            else if (rhsLog == null)
                ++rhsLogIndex;
            else
            {
                result = this.CompareLogs(lhsLog, rhsLog);
                if (result != 0)
                    return result;
                ++lhsLogIndex;
                ++rhsLogIndex;
            }
        }
        
        // compare types
        result = lhs.Type.CompareTo(rhs.Type);
        if (result != 0)
            return result;
        
        // compare IDs
        return lhs.Id - rhs.Id;
    }


    // Copy selected log analysis results to clipboard.
    async void CopySelectedAnalysisResults()
    {
        // check state
        var count = this.selectedAnalysisResults.Count;
        if (count <= 0)
            return;
        
        // generate text
        var textBuffer = new StringBuilder();
        for (var i = 0; i < count; ++i)
        {
            if (i > 0)
                textBuffer.AppendLine();
            textBuffer.Append(this.selectedAnalysisResults[i].Message);
        }

        // set to clipboard
        try
        {
            await App.Current.Clipboard!.SetTextAsync(textBuffer.ToString());
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to set text of log analysis results to clipboard");
            this.GenerateErrorMessage(this.Application.GetStringNonNull("LogAnalysisViewModel.FailedToCopySelectedAnalysisResults"));
        }
    }


    /// <summary>
    /// Command to copy selected log analysis results to clipboard.
    /// </summary>
    public ICommand CopySelectedAnalysisResultsCommand { get; }


    // Detach from given log analyzer.
    void DetachFromAnalyzer(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
    {
        // remove from set
        if (!this.attachedAnalyzers.Remove(analyzer))
            return;

        // remove handlers
        if (analyzer.AnalysisResults is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged -= this.OnAnalyzerAnalysisResultsChanged;
        analyzer.ErrorMessageGenerated -= this.OnAnalyzerErrorMessageGenerated;
        analyzer.PropertyChanged -= this.OnAnalyzerPropertyChanged;

        // remove analysis results
        this.analysisResults.RemoveAll(analyzer.AnalysisResults);

        // report state
        this.updateAnalysisStateAction?.Schedule();
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // detach from session
        this.Session.AllLogReadersDisposed -= this.OnAllLogReadersDisposed;
        this.displayLogPropertiesObserverToken.Dispose();

        // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Check whether at least one analysis result is selected or not.
    /// </summary>
    public bool HasSelectedAnalysisResults { get => this.GetValue(HasSelectedAnalysisResultsProperty); }


    /// <summary>
    /// Check whether logs are being analyzed or not.
    /// </summary>
    public bool IsAnalyzing { get => this.GetValue(IsAnalyzingProperty); }


    /// <summary>
    /// Get or set whether panel of log analysis is visible or not.
    /// </summary>
    public bool IsPanelVisible 
    {
        get => this.GetValue(IsPanelVisibleProperty);
        set => this.SetValue(IsPanelVisibleProperty, value);
    }


    /// <summary>
    /// Get list of <see cref="KeyLogAnalysisRuleSet"/> for log analysis.
    /// </summary>
    public IList<KeyLogAnalysisRuleSet> KeyLogAnalysisRuleSets { get => this.keyLogAnalysisRuleSets; }


    /// <summary>
    /// Get list of script sets to analyze log.
    /// </summary>
    public IList<LogAnalysisScriptSet> LogAnalysisScriptSets { get => this.logAnalysisScriptSets; }


    /// <inheritdoc/>
    public override long MemorySize 
    {
        get
        {
            var size = (long)(this.analysisResults.Count * IntPtr.Size);
            foreach (var analyzer in this.attachedAnalyzers)
                size += analyzer.MemorySize;
            return size;
        }
    }


    // Called when all log readers have been disposed.
    void OnAllLogReadersDisposed()
    {
        if (this.IsDisposed)
            return;
        this.updateKeyLogAnalysisAction.Reschedule();
        this.updateOperationCountingAnalysisAction.Reschedule();
        this.updateOperationDurationAnalysisAction.Reschedule();
        this.updateScriptLogAnalysis.Reschedule();
    }


    // Called when list of analysis results changed.
    void OnAnalysisResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogAnalysisResult>().Let(it =>
                {
                    if (it.Count == 1)
                        this.selectedAnalysisResults.Remove(it[0]);
                    else
                        this.selectedAnalysisResults.RemoveAll(it, true);
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                this.selectedAnalysisResults.Clear();
                break;
        }
    }


    // Called when analysis results of log analyzer changed.
    void OnAnalyzerAnalysisResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.analysisResults.AddAll(e.NewItems!.Cast<DisplayableLogAnalysisResult>());
                break;
            case NotifyCollectionChangedAction.Remove:
                this.analysisResults.RemoveAll(e.OldItems!.Cast<DisplayableLogAnalysisResult>());
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var analyzer in this.attachedAnalyzers)
                {
                    if (analyzer.AnalysisResults == sender)
                    {
                        this.analysisResults.RemoveAll(it => it.Analyzer == analyzer);
                        this.analysisResults.AddAll(analyzer.AnalysisResults);
                        break;
                    }
                }
                break;
            default:
#if DEBUG
                throw new NotSupportedException();
#else
                this.Logger.LogError($"Unsupported change action of list of log analysis result: {e.Action}");
                break;
#endif
        }
    }


    // Called when error message generated by log analyzer.
    void OnAnalyzerErrorMessageGenerated(IDisplayableLogProcessor analyzer, MessageEventArgs e) =>
        this.GenerateErrorMessage(e.Message);
    

    // Called when property of log analyzer changed.
    void OnAnalyzerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>.IsProcessing):
            case nameof(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>.Progress):
                this.updateAnalysisStateAction.Schedule(LogsAnalysisStateUpdateDelay);
                break;
        }
    }


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        // call base
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);

        // reset log analysis rule sets
        if (this.Settings.GetValueOrDefault(SettingKeys.ResetLogAnalysisRuleSetsAfterSettingLogProfile))
        {
            this.keyLogAnalysisRuleSets.Clear();
            this.logAnalysisScriptSets.Clear();
            this.operationDurationAnalysisRuleSets.Clear();
        }
    }


    /// <inheritdoc/>
    protected override void OnRestoreState(JsonElement element)
    {
        // call base
        this.isRestoringState = true;
        base.OnRestoreState(element);

        // restore rule sets
        this.keyLogAnalysisRuleSets.Clear();
        this.operationDurationAnalysisRuleSets.Clear();
        this.logAnalysisScriptSets.Clear();
        if ((element.TryGetProperty(nameof(KeyLogAnalysisRuleSet), out var jsonValue) // for upgrade case
            || element.TryGetProperty($"LogAnalysis.{nameof(KeyLogAnalysisRuleSet)}", out jsonValue))
                && jsonValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var jsonId in jsonValue.EnumerateArray())
            {
                var ruleSet = jsonId.ValueKind == JsonValueKind.String
                    ? KeyLogAnalysisRuleSetManager.Default.GetRuleSetOrDefault(jsonId.GetString()!)
                    : null;
                if (ruleSet != null)
                    this.keyLogAnalysisRuleSets.Add(ruleSet);
            }
        }
        if ((element.TryGetProperty(nameof(LogAnalysisScriptSets), out jsonValue) // for upgrade case
            || element.TryGetProperty($"LogAnalysis.{nameof(LogAnalysisScriptSets)}", out jsonValue))
                && jsonValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var jsonId in jsonValue.EnumerateArray())
            {
                var scriptSet = jsonId.ValueKind == JsonValueKind.String
                    ? LogAnalysisScriptSetManager.Default.GetScriptSetOrDefault(jsonId.GetString()!)
                    : null;
                if (scriptSet != null)
                    this.logAnalysisScriptSets.Add(scriptSet);
            }
        }
        if (element.TryGetProperty($"LogAnalysis.{nameof(OperationCountingAnalysisRuleSets)}", out jsonValue)
                && jsonValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var jsonId in jsonValue.EnumerateArray())
            {
                var ruleSet = jsonId.ValueKind == JsonValueKind.String
                    ? OperationCountingAnalysisRuleSetManager.Default.GetRuleSetOrDefault(jsonId.GetString()!)
                    : null;
                if (ruleSet != null)
                    this.operationCountingAnalysisRuleSets.Add(ruleSet);
            }
        }
        if ((element.TryGetProperty(nameof(OperationDurationAnalysisRuleSets), out jsonValue) // for upgrade case
            || element.TryGetProperty($"LogAnalysis.{nameof(OperationDurationAnalysisRuleSets)}", out jsonValue)) 
                && jsonValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var jsonId in jsonValue.EnumerateArray())
            {
                var ruleSet = jsonId.ValueKind == JsonValueKind.String
                    ? OperationDurationAnalysisRuleSetManager.Default.GetRuleSetOrDefault(jsonId.GetString()!)
                    : null;
                if (ruleSet != null)
                    this.operationDurationAnalysisRuleSets.Add(ruleSet);
            }
        }

        // restore panel state
        if (element.TryGetProperty("IsLogAnalysisPanelVisible", out jsonValue) // for upgrade case
            || element.TryGetProperty($"LogAnalysis.{nameof(IsPanelVisible)}", out jsonValue))
        {
            this.SetValue(IsPanelVisibleProperty, jsonValue.ValueKind != JsonValueKind.False);
        }
        if ((element.TryGetProperty("LogAnalysisPanelSize", out jsonValue) // for upgrade case
            || element.TryGetProperty($"LogAnalysis.{nameof(PanelSize)}", out jsonValue))
                && jsonValue.TryGetDouble(out var doubleValue)
                && PanelSizeProperty.ValidationFunction(doubleValue) == true)
        {
            this.SetValue(PanelSizeProperty, doubleValue);
        }

        // complete
        this.isRestoringState = false;
    }


    /// <inheritdoc/>
    protected override void OnSaveState(Utf8JsonWriter writer)
    {
        // save rule sets
        if (this.keyLogAnalysisRuleSets.IsNotEmpty())
        {
            var idSet = new HashSet<string>();
            writer.WritePropertyName($"LogAnalysis.{nameof(KeyLogAnalysisRuleSet)}");
            writer.WriteStartArray();
            foreach (var ruleSet in this.keyLogAnalysisRuleSets)
            {
                if (idSet.Add(ruleSet.Id))
                    writer.WriteStringValue(ruleSet.Id);
            }
            writer.WriteEndArray();
        }
        if (this.logAnalysisScriptSets.IsNotEmpty())
        {
            var idSet = new HashSet<string>();
            writer.WritePropertyName($"LogAnalysis.{nameof(LogAnalysisScriptSets)}");
            writer.WriteStartArray();
            foreach (var scriptSet in this.logAnalysisScriptSets)
            {
                if (idSet.Add(scriptSet.Id))
                    writer.WriteStringValue(scriptSet.Id);
            }
            writer.WriteEndArray();
        }
        if (this.operationCountingAnalysisRuleSets.IsNotEmpty())
        {
            var idSet = new HashSet<string>();
            writer.WritePropertyName($"LogAnalysis.{nameof(OperationCountingAnalysisRuleSets)}");
            writer.WriteStartArray();
            foreach (var ruleSet in this.operationCountingAnalysisRuleSets)
            {
                if (idSet.Add(ruleSet.Id))
                    writer.WriteStringValue(ruleSet.Id);
            }
            writer.WriteEndArray();
        }
        if (this.operationDurationAnalysisRuleSets.IsNotEmpty())
        {
            var idSet = new HashSet<string>();
            writer.WritePropertyName($"LogAnalysis.{nameof(OperationDurationAnalysisRuleSets)}");
            writer.WriteStartArray();
            foreach (var ruleSet in this.operationDurationAnalysisRuleSets)
            {
                if (idSet.Add(ruleSet.Id))
                    writer.WriteStringValue(ruleSet.Id);
            }
            writer.WriteEndArray();
        }

        // save panel state
        writer.WriteBoolean($"LogAnalysis.{nameof(IsPanelVisible)}", this.IsPanelVisible);
        writer.WriteNumber($"LogAnalysis.{nameof(PanelSize)}", this.PanelSize);

        // call base
        base.OnSaveState(writer);
    }


    // Called when selected analysis results changed.
    void OnSelectedAnalysisResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.reportSelectedAnalysisResultsInfoAction.Schedule();
        if (!this.IsDisposed)
            this.SetValue(HasSelectedAnalysisResultsProperty, this.selectedAnalysisResults.IsNotEmpty());
    }


    /// <summary>
    /// Get list of <see cref="OperationCountingAnalysisRuleSet"/> for log analysis.
    /// </summary>
    public IList<OperationCountingAnalysisRuleSet> OperationCountingAnalysisRuleSets { get => this.operationCountingAnalysisRuleSets; }


    /// <summary>
    /// Get list of <see cref="OperationDurationAnalysisRuleSet"/> for log analysis.
    /// </summary>
    public IList<OperationDurationAnalysisRuleSet> OperationDurationAnalysisRuleSets { get => this.operationDurationAnalysisRuleSets; }


    /// <summary>
    /// Get or set size of panel of log analysis.
    /// </summary>
    public double PanelSize
    {
        get => this.GetValue(PanelSizeProperty);
        set => this.SetValue(PanelSizeProperty, value);
    }


    /// <summary>
    /// Get collection of selected analysis results.
    /// </summary>
    public IList<DisplayableLogAnalysisResult> SelectedAnalysisResults { get => this.selectedAnalysisResults; }


    /// <summary>
    /// Get average byte size of selcted analysis results.
    /// </summary>
    public long? SelectedAnalysisResultsAverageByteSize { get => this.GetValue(SelectedAnalysisResultsAverageByteSizeProperty); }


    /// <summary>
    /// Get average duration of selcted analysis results.
    /// </summary>
    public TimeSpan? SelectedAnalysisResultsAverageDuration { get => this.GetValue(SelectedAnalysisResultsAverageDurationProperty); }


    /// <summary>
    /// Get average quantity of selcted analysis results.
    /// </summary>
    public double? SelectedAnalysisResultsAverageQuantity { get => this.GetValue(SelectedAnalysisResultsAverageQuantityProperty); }


    /// <summary>
    /// Get total byte size of selcted analysis results.
    /// </summary>
    public long? SelectedAnalysisResultsTotalByteSize { get => this.GetValue(SelectedAnalysisResultsTotalByteSizeProperty); }


    /// <summary>
    /// Get total duration of selcted analysis results.
    /// </summary>
    public TimeSpan? SelectedAnalysisResultsTotalDuration { get => this.GetValue(SelectedAnalysisResultsTotalDurationProperty); }


    /// <summary>
    /// Get total quantity of selcted analysis results.
    /// </summary>
    public long? SelectedAnalysisResultsTotalQuantity { get => this.GetValue(SelectedAnalysisResultsTotalQuantityProperty); }
}