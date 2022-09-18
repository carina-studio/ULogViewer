using System;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ViewModels;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log analysis.
/// </summary>
class LogAnalysisViewModel : SessionComponent
{
    /// <summary>
    /// Property of <see cref="IsAnalyzingLogs"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsAnalyzingLogsProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(IsAnalyzingLogs));
    /// <summary>
    /// Property of <see cref="IsPanelVisible"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsPanelVisibleProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(IsPanelVisible), false);
    /// <summary>
    /// Property of <see cref="LogAnalysisProgress"/>.
    /// </summary>
    public static readonly ObservableProperty<double> LogAnalysisProgressProperty = ObservableProperty.Register<LogAnalysisViewModel, double>(nameof(LogAnalysisProgress));
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


    // Constants.
    const int LogsAnalysisStateUpdateDelay = 300;


    // Static fields.
    static readonly SettingKey<double> latestPanelSizeKey = new SettingKey<double>("Session.LatestLogAnalysisPanelSize", PanelSizeProperty.DefaultValue);
    [Obsolete]
	static readonly SettingKey<double> latestSidePanelSizeKey = new SettingKey<double>("Session.LatestSidePanelSize", Session.MarkedLogsPanelSizeProperty.DefaultValue);

    
    // Fields.
    readonly HashSet<IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>> attachedLogAnalyzers = new();
    readonly IDisposable displayLogPropertiesObserverToken;
    bool isRestoringState;
    readonly ObservableList<KeyLogAnalysisRuleSet> keyLogAnalysisRuleSets = new();
    readonly KeyLogDisplayableLogAnalyzer keyLogAnalyzer;
    readonly DisplayableLog?[] logAnalysisResultComparisonTempLogs1 = new DisplayableLog?[3];
    readonly DisplayableLog?[] logAnalysisResultComparisonTempLogs2 = new DisplayableLog?[3];
    readonly SortedObservableList<DisplayableLogAnalysisResult> logAnalysisResults;
	readonly ObservableList<LogAnalysisScriptSet> logAnalysisScriptSets = new();
    readonly IDisposable logProfileObserverToken;
    readonly ObservableList<OperationDurationAnalysisRuleSet> operationDurationAnalysisRuleSets = new();
	readonly OperationDurationDisplayableLogAnalyzer operationDurationAnalyzer;
    readonly ScriptDisplayableLogAnalyzer scriptLogAnalyzer;
    readonly ScheduledAction updateKeyLogAnalysisAction;
    readonly ScheduledAction updateLogsAnalysisStateAction;
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

        // create collections
        this.logAnalysisResults = new(this.CompareLogAnalysisResults);

        // create analyzers
        this.keyLogAnalysisRuleSets.CollectionChanged += (_, e) => 
            this.updateKeyLogAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.keyLogAnalyzer = new KeyLogDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToLogAnalyzer(it));
        this.logAnalysisScriptSets.CollectionChanged += (_, e) =>
            this.updateScriptLogAnalysis?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.operationDurationAnalysisRuleSets.CollectionChanged += (_, e) => 
            this.updateOperationDurationAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        this.operationDurationAnalyzer = new OperationDurationDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToLogAnalyzer(it));
        this.scriptLogAnalyzer = new ScriptDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToLogAnalyzer(it));
        
        // setup properties
        this.LogAnalysisResults = this.logAnalysisResults.AsReadOnly();

        // create scheduled actions
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
        this.updateLogsAnalysisStateAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            var isAnalyzing = false;
            var progress = 1.0;
            foreach (var analyzer in this.attachedLogAnalyzers)
            {
                if (analyzer.IsProcessing)
                {
                    isAnalyzing = true;
                    progress = Math.Min(progress, analyzer.Progress);
                }
            }
            if (isAnalyzing)
            {
                this.SetValue(IsAnalyzingLogsProperty, true);
                this.SetValue(LogAnalysisProgressProperty, progress);
            }
            else
            {
                this.SetValue(IsAnalyzingLogsProperty, false);
                this.SetValue(LogAnalysisProgressProperty, 0);
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
            this.operationDurationAnalyzer.LogProperties.Clear();
            this.operationDurationAnalyzer.LogProperties.AddAll(properties);
            this.scriptLogAnalyzer.LogProperties.Clear();
            this.scriptLogAnalyzer.LogProperties.AddAll(properties);
        });
        this.logProfileObserverToken = session.GetValueAsObservable(Session.LogProfileProperty).Subscribe(_ =>
        {
            // reset log analysis rule sets
			if (this.Settings.GetValueOrDefault(SettingKeys.ResetLogAnalysisRuleSetsAfterSettingLogProfile))
			{
				this.keyLogAnalysisRuleSets.Clear();
				this.logAnalysisScriptSets.Clear();
				this.operationDurationAnalysisRuleSets.Clear();
			}
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


    // Attach to given log analyzer.
    void AttachToLogAnalyzer(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
    {
        // add to set
        if (!this.attachedLogAnalyzers.Add(analyzer))
            return;
        
        // add handlers
        if (analyzer.AnalysisResults is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged += this.OnLogAnalyzerAnalysisResultsChanged;
        analyzer.ErrorMessageGenerated += this.OnLogAnalyzerErrorMessageGenerated;
        analyzer.PropertyChanged += this.OnLogAnalyzerPropertyChanged;

        // add analysis results
        this.logAnalysisResults.AddAll(analyzer.AnalysisResults);

        // report state
        this.updateLogsAnalysisStateAction?.Schedule();
    }


    // Compare log analysis results.
    int CompareLogAnalysisResults(DisplayableLogAnalysisResult? lhs, DisplayableLogAnalysisResult? rhs)
    {
        // get logs
        var lhsLogs = this.logAnalysisResultComparisonTempLogs1;
        lhsLogs[0] = lhs!.BeginningLog;
        lhsLogs[1] = lhs.Log;
        lhsLogs[2] = lhs.EndingLog;
        var rhsLogs = this.logAnalysisResultComparisonTempLogs2;
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


    // Detach from given log analyzer.
    void DetachFromLogAnalyzer(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
    {
        // remove from set
        if (!this.attachedLogAnalyzers.Remove(analyzer))
            return;

        // remove handlers
        if (analyzer.AnalysisResults is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged -= this.OnLogAnalyzerAnalysisResultsChanged;
        analyzer.ErrorMessageGenerated -= this.OnLogAnalyzerErrorMessageGenerated;
        analyzer.PropertyChanged -= this.OnLogAnalyzerPropertyChanged;

        // remove analysis results
        this.logAnalysisResults.RemoveAll(analyzer.AnalysisResults);

        // report state
        this.updateLogsAnalysisStateAction?.Schedule();
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
    /// Check whether logs are being analyzed or not.
    /// </summary>
    public bool IsAnalyzingLogs { get => this.GetValue(IsAnalyzingLogsProperty); }


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
    /// Get progress of logs analysis.
    /// </summary>
    public double LogAnalysisProgress { get => this.GetValue(LogAnalysisProgressProperty); }


    /// <summary>
    /// Get list of result of log analysis.
    /// </summary>
    public IList<DisplayableLogAnalysisResult> LogAnalysisResults { get; }


    /// <summary>
    /// Get list of script sets to analyze log.
    /// </summary>
    public IList<LogAnalysisScriptSet> LogAnalysisScriptSets { get => this.logAnalysisScriptSets; }


    /// <inheritdoc/>
    public override long MemorySize 
    {
        get
        {
            var size = (long)(this.logAnalysisResults.Count * IntPtr.Size);
            foreach (var analyzer in this.attachedLogAnalyzers)
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
        this.updateOperationDurationAnalysisAction.Reschedule();
        this.updateScriptLogAnalysis.Reschedule();
    }


    // Called when analysis results of log analyzer changed.
    void OnLogAnalyzerAnalysisResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.logAnalysisResults.AddAll(e.NewItems!.Cast<DisplayableLogAnalysisResult>());
                break;
            case NotifyCollectionChangedAction.Remove:
                this.logAnalysisResults.RemoveAll(e.OldItems!.Cast<DisplayableLogAnalysisResult>());
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var analyzer in this.attachedLogAnalyzers)
                {
                    if (analyzer.AnalysisResults == sender)
                    {
                        this.logAnalysisResults.RemoveAll(it => it.Analyzer == analyzer);
                        this.logAnalysisResults.AddAll(analyzer.AnalysisResults);
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
    void OnLogAnalyzerErrorMessageGenerated(IDisplayableLogProcessor analyzer, MessageEventArgs e) =>
        this.GenerateErrorMessage(e.Message);
    

    // Called when property of log analyzer changed.
    void OnLogAnalyzerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>.IsProcessing):
            case nameof(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>.Progress):
                this.updateLogsAnalysisStateAction.Schedule(LogsAnalysisStateUpdateDelay);
                break;
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
        if (element.TryGetProperty(nameof(KeyLogAnalysisRuleSet), out var jsonValue) && jsonValue.ValueKind == JsonValueKind.Array)
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
        if (element.TryGetProperty(nameof(LogAnalysisScriptSet), out jsonValue) && jsonValue.ValueKind == JsonValueKind.Array)
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
        if (element.TryGetProperty(nameof(OperationDurationAnalysisRuleSets), out jsonValue) && jsonValue.ValueKind == JsonValueKind.Array)
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
            || element.TryGetProperty(nameof(IsPanelVisible), out jsonValue))
        {
            this.SetValue(IsPanelVisibleProperty, jsonValue.ValueKind != JsonValueKind.False);
        }
        if ((element.TryGetProperty("LogAnalysisPanelSize", out jsonValue) // for upgrade case
            || element.TryGetProperty(nameof(PanelSize), out jsonValue))
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
            writer.WritePropertyName(nameof(KeyLogAnalysisRuleSet));
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
            writer.WritePropertyName(nameof(LogAnalysisScriptSets));
            writer.WriteStartArray();
            foreach (var scriptSet in this.logAnalysisScriptSets)
            {
                if (idSet.Add(scriptSet.Id))
                    writer.WriteStringValue(scriptSet.Id);
            }
            writer.WriteEndArray();
        }
        if (this.operationDurationAnalysisRuleSets.IsNotEmpty())
        {
            var idSet = new HashSet<string>();
            writer.WritePropertyName(nameof(OperationDurationAnalysisRuleSets));
            writer.WriteStartArray();
            foreach (var ruleSet in this.operationDurationAnalysisRuleSets)
            {
                if (idSet.Add(ruleSet.Id))
                    writer.WriteStringValue(ruleSet.Id);
            }
            writer.WriteEndArray();
        }

        // save panel state
        writer.WriteBoolean(nameof(IsPanelVisible), this.IsPanelVisible);
        writer.WriteNumber(nameof(PanelSize), this.PanelSize);

        // call base
        base.OnSaveState(writer);
    }


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
}