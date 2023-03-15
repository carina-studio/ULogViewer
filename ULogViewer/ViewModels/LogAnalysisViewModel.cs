using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log analysis.
/// </summary>
class LogAnalysisViewModel : SessionComponent, IScriptRunningHost
{
    /// <summary>
    /// Property of <see cref="AnalysisProgress"/>.
    /// </summary>
    public static readonly ObservableProperty<double> AnalysisProgressProperty = ObservableProperty.Register<LogAnalysisViewModel, double>(nameof(AnalysisProgress));
    /// <summary>
    /// Property of <see cref="HasCooperativeLogAnalysisScriptSet"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> HasCooperativeLogAnalysisScriptSetProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(HasCooperativeLogAnalysisScriptSet));
    /// <summary>
    /// Property of <see cref="HasNewAnalysisResultsInBackground"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> HasNewAnalysisResultsInBackgroundProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(HasNewAnalysisResultsInBackground));
    /// <summary>
    /// Property of <see cref="HasSelectedAnalysisResults"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> HasSelectedAnalysisResultsProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(HasSelectedAnalysisResults));
    /// <summary>
    /// Property of <see cref="IsAnalyzing"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsAnalyzingProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(IsAnalyzing));
    /// <summary>
    /// Property of <see cref="IsCooperativeLogAnalysisScriptSetEnabled"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsCooperativeLogAnalysisScriptSetEnabledProperty = ObservableProperty.Register<LogAnalysisViewModel, bool>(nameof(IsCooperativeLogAnalysisScriptSetEnabled), true);
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
    static readonly SettingKey<double> latestPanelSizeKey = new("Session.LatestLogAnalysisPanelSize", PanelSizeProperty.DefaultValue);
    [Obsolete]
	static readonly SettingKey<double> latestSidePanelSizeKey = new("Session.LatestSidePanelSize", Session.MarkedLogsPanelSizeProperty.DefaultValue);

    
    // Fields.
    readonly DisplayableLog?[] analysisResultComparisonTempLogs1 = new DisplayableLog?[3];
    readonly DisplayableLog?[] analysisResultComparisonTempLogs2 = new DisplayableLog?[3];
    readonly SortedObservableList<DisplayableLogAnalysisResult> analysisResults;
    readonly HashSet<IDisplayableLogAnalyzer<DisplayableLogAnalysisResult>> attachedAnalyzers = new();
    readonly ScriptDisplayableLogAnalyzer coopScriptLogAnalyzer;
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
    readonly ScheduledAction updateCoopScriptLogAnalysisAction;
    readonly ScheduledAction updateKeyLogAnalysisAction;
    readonly ScheduledAction updateOperationCountingAnalysisAction;
    readonly ScheduledAction updateOperationDurationAnalysisAction;
    readonly ScheduledAction updateScriptLogAnalysisAction;
    

    /// <summary>
    /// Initialize new <see cref="LogAnalysisViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogAnalysisViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
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
        this.coopScriptLogAnalyzer = new ScriptDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
        {
            it.IsCooperativeLogAnalysis = true;
            this.AttachToAnalyzer(it);
        });
        this.keyLogAnalysisRuleSets.CollectionChanged += (_, e) => 
        {
            if (this.keyLogAnalysisRuleSets.IsEmpty())
                this.Logger.LogTrace("Clear key log analysis rule sets");
            else
                this.Logger.LogTrace("Change key log analysis rule sets: {ruleSetsCount}", this.keyLogAnalysisRuleSets.Count);
            this.updateKeyLogAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        };
        this.keyLogAnalyzer = new KeyLogDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        this.logAnalysisScriptSets.CollectionChanged += (_, e) =>
        {
            if (this.logAnalysisScriptSets.IsEmpty())
                this.Logger.LogTrace("Clear log analysis script sets");
            else
                this.Logger.LogTrace("Change log analysis script sets: {scriptSetsCount}", this.logAnalysisScriptSets.Count);
            this.updateScriptLogAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        };
        this.operationCountingAnalysisRuleSets.CollectionChanged += (_, e) => 
        {
            if (this.operationCountingAnalysisRuleSets.IsEmpty())
                this.Logger.LogTrace("Clear operation counting analysis rule sets");
            else
                this.Logger.LogTrace("Change operation counting analysis rule sets: {ruleSetsCount}", this.operationCountingAnalysisRuleSets.Count);
            this.updateOperationCountingAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        };
        this.operationCountingAnalyzer = new OperationCountingAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        this.operationDurationAnalysisRuleSets.CollectionChanged += (_, e) => 
        {
            if (this.operationCountingAnalysisRuleSets.IsEmpty())
                this.Logger.LogTrace("Clear operation duration analysis rule sets");
            else
                this.Logger.LogTrace("Change operation duration analysis rule sets: {ruleSetsCount}", this.operationCountingAnalysisRuleSets.Count);
            this.updateOperationDurationAnalysisAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisParamsUpdateDelay));
        };
        this.operationDurationAnalyzer = new OperationDurationDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        this.scriptLogAnalyzer = new ScriptDisplayableLogAnalyzer(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
            this.AttachToAnalyzer(it));
        
        // setup properties
        this.AnalysisResults = new Collections.SafeReadOnlyList<DisplayableLogAnalysisResult>(this.analysisResults);

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
        this.updateCoopScriptLogAnalysisAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            this.coopScriptLogAnalyzer.ScriptSets.Clear();
            if (this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty) 
                && this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript)
                && this.Application.ProductManager.IsProductActivated(Products.Professional))
            {
                var scriptSet = this.LogProfile?.CooperativeLogAnalysisScriptSet;
                if (scriptSet != null)
                {
                    this.Logger.LogTrace("Apply cooperative log analysis script set");
                    this.LogProfile?.CooperativeLogAnalysisScriptSet?.Let(it => this.coopScriptLogAnalyzer.ScriptSets.Add(it));
                }
                else
                    this.Logger.LogTrace("Clear cooperative log analysis script set");
            }
            else
                this.Logger.LogTrace("Cooperative log analysis script set has been disabled");
        });
        this.updateKeyLogAnalysisAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.keyLogAnalyzer.RuleSets.SequenceEqual(this.keyLogAnalysisRuleSets))
            {
                this.Logger.LogTrace("Update key log analysis with {ruleSetsCount} rule sets", this.keyLogAnalysisRuleSets.Count);
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
                this.Logger.LogTrace("Update operation counting analysis with {ruleSetsCount} rule sets", this.operationCountingAnalysisRuleSets.Count);
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
                this.Logger.LogTrace("Update operation duration analysis with {ruleSetsCount} rule sets", this.operationDurationAnalysisRuleSets.Count);
                this.operationDurationAnalyzer.RuleSets.Clear();
                this.operationDurationAnalyzer.RuleSets.AddAll(this.operationDurationAnalysisRuleSets);
            }
        });
        this.updateScriptLogAnalysisAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.scriptLogAnalyzer.ScriptSets.SequenceEqual(this.logAnalysisScriptSets))
            {
                this.scriptLogAnalyzer.ScriptSets.Clear();
                if (this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript) 
                    && this.Application.ProductManager.IsProductActivated(Products.Professional))
                {
                    this.Logger.LogTrace("Update log analysis with {scriptSetsCount} script sets", this.logAnalysisScriptSets.Count);
                    this.scriptLogAnalyzer.ScriptSets.AddAll(this.logAnalysisScriptSets);
                }
                else
                    this.Logger.LogTrace("Update log analysis with 0 script sets");
            }
        });

        // attach to self properties
        this.GetValueAsObservable(IsCooperativeLogAnalysisScriptSetEnabledProperty).Subscribe(isEnabled =>
        {
            if (isInit)
                return;
            if (isEnabled)
                this.updateCoopScriptLogAnalysisAction.Schedule();
            else
                this.updateCoopScriptLogAnalysisAction.Execute();
        });
        this.GetValueAsObservable(IsPanelVisibleProperty).Subscribe(isVisible =>
        {
            if (!isInit && isVisible)
                this.ResetValue(HasNewAnalysisResultsInBackgroundProperty);
        });
        this.GetValueAsObservable(PanelSizeProperty).Subscribe(size =>
        {
            if (!isInit && !this.isRestoringState)
                this.PersistentState.SetValue<double>(latestPanelSizeKey, size);
        });
        
        // attach to session
        session.AllLogReadersDisposed += this.OnAllLogReadersDisposed;
        this.displayLogPropertiesObserverToken = session.GetValueAsObservable(Session.DisplayLogPropertiesProperty).Subscribe(properties =>
        {
            this.coopScriptLogAnalyzer.LogProperties.Clear();
            this.coopScriptLogAnalyzer.LogProperties.AddAll(properties);
            this.keyLogAnalyzer.LogProperties.Clear();
            this.keyLogAnalyzer.LogProperties.AddAll(properties);
            this.operationCountingAnalyzer.LogProperties.Clear();
            this.operationCountingAnalyzer.LogProperties.AddAll(properties);
            this.operationDurationAnalyzer.LogProperties.Clear();
            this.operationDurationAnalyzer.LogProperties.AddAll(properties);
            this.scriptLogAnalyzer.LogProperties.Clear();
            this.scriptLogAnalyzer.LogProperties.AddAll(properties);
        });

        // attach to product manager
        this.Application.ProductManager.ProductActivationChanged += this.OnProductActivationChanged;

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


    /// <inheritdoc/>
    IAppSuiteApplication IApplicationObject<IAppSuiteApplication>.Application => this.Application;


    // Attach to given log analyzer.
    void AttachToAnalyzer(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
    {
        // add to set
        if (!this.attachedAnalyzers.Add(analyzer))
            return;
        
        // log
        if (this.Application.IsDebugMode)
            this.Logger.LogDebug("Attach to analyzer {analyzer}", analyzer);
        
        // add handlers
        if (analyzer.AnalysisResults is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged += this.OnAnalyzerAnalysisResultsChanged;
        analyzer.ErrorMessageGenerated += this.OnAnalyzerErrorMessageGenerated;
        analyzer.PropertyChanged += this.OnAnalyzerPropertyChanged;
        if (analyzer is IScriptRunningHost scriptRunningHost)
            scriptRunningHost.ScriptRuntimeErrorOccurred += this.OnAnalyzerScriptRuntimeErrorOccurred;

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
    unsafe int CompareAnalysisResults(DisplayableLogAnalysisResult? lhs, DisplayableLogAnalysisResult? rhs)
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
        int result;
        while (lhsLogIndex < logCount && rhsLogIndex < logCount)
        {
            var lhsLog = lhsLogs[lhsLogIndex];
            var rhsLog = rhsLogs[rhsLogIndex];
            if (lhsLog == null)
            {
                ++lhsLogIndex;
                if (rhsLog == null)
                    ++rhsLogIndex;
            }
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
        result = (int)lhs.Type - (int)rhs.Type;
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
    void DetachFromAnalyzer(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer, bool isDisposing)
    {
        // remove from set
        if (!this.attachedAnalyzers.Remove(analyzer))
            return;
        
        // log
        if (this.Application.IsDebugMode)
            this.Logger.LogDebug("Detach from analyzer {analyzer}", analyzer);

        // remove handlers
        if (analyzer.AnalysisResults is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged -= this.OnAnalyzerAnalysisResultsChanged;
        analyzer.ErrorMessageGenerated -= this.OnAnalyzerErrorMessageGenerated;
        analyzer.PropertyChanged -= this.OnAnalyzerPropertyChanged;
        if (analyzer is IScriptRunningHost scriptRunningHost)
            scriptRunningHost.ScriptRuntimeErrorOccurred -= this.OnAnalyzerScriptRuntimeErrorOccurred;

        // remove analysis results
        if (!isDisposing)
            this.analysisResults.RemoveAll(analyzer.AnalysisResults);

        // report state
        if (!isDisposing)
            this.updateAnalysisStateAction?.Schedule();
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // detach from product manager
        this.Application.ProductManager.ProductActivationChanged -= this.OnProductActivationChanged;

        // detach from session
        this.Session.AllLogReadersDisposed -= this.OnAllLogReadersDisposed;
        this.displayLogPropertiesObserverToken.Dispose();

        // detach from analyzers
        this.DetachFromAnalyzer(this.coopScriptLogAnalyzer, true);
        this.DetachFromAnalyzer(this.keyLogAnalyzer, true);
        this.DetachFromAnalyzer(this.operationCountingAnalyzer, true);
        this.DetachFromAnalyzer(this.operationDurationAnalyzer, true);
        this.DetachFromAnalyzer(this.scriptLogAnalyzer, true);
        this.coopScriptLogAnalyzer.Dispose();
        this.keyLogAnalyzer.Dispose();
        this.operationCountingAnalyzer.Dispose();
        this.operationDurationAnalyzer.Dispose();
        this.scriptLogAnalyzer.Dispose();

        // clear analysis results
        this.analysisResults.Clear();
        this.updateAnalysisStateAction.Cancel();

        // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Check whether cooperative log analysis script set has been set to current log profile or not.
    /// </summary>
    public bool HasCooperativeLogAnalysisScriptSet { get => this.GetValue(HasCooperativeLogAnalysisScriptSetProperty); }


    /// <summary>
    /// Check whether at least one new analysis result has been generated in background or not.
    /// </summary>
    public bool HasNewAnalysisResultsInBackground { get => this.GetValue(HasNewAnalysisResultsInBackgroundProperty); }


    /// <summary>
    /// Check whether at least one analysis result is selected or not.
    /// </summary>
    public bool HasSelectedAnalysisResults { get => this.GetValue(HasSelectedAnalysisResultsProperty); }


    /// <summary>
    /// Check whether logs are being analyzed or not.
    /// </summary>
    public bool IsAnalyzing { get => this.GetValue(IsAnalyzingProperty); }


    /// <summary>
    /// Get or set whether cooperative log analysis script set of current log profile is enabled or not.
    /// </summary>
    public bool IsCooperativeLogAnalysisScriptSetEnabled 
    { 
        get => this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty); 
        set => this.SetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty, value);
    }


    /// <summary>
    /// Get or set whether panel of log analysis is visible or not.
    /// </summary>
    public bool IsPanelVisible 
    {
        get => this.GetValue(IsPanelVisibleProperty);
        set => this.SetValue(IsPanelVisibleProperty, value);
    }


    /// <inheritdoc/>
    bool IScriptRunningHost.IsRunningScripts => ((IScriptRunningHost)this.coopScriptLogAnalyzer).IsRunningScripts
                    || ((IScriptRunningHost)this.scriptLogAnalyzer).IsRunningScripts;


    /// <summary>
    /// Get list of <see cref="KeyLogAnalysisRuleSet"/> for log analysis.
    /// </summary>
    public IList<KeyLogAnalysisRuleSet> KeyLogAnalysisRuleSets { get => this.keyLogAnalysisRuleSets; }


    /// <summary>
    /// Runtime error was occurred in one of log analysis script.
    /// </summary>
    public event EventHandler<ScriptRuntimeErrorEventArgs>? LogAnalysisScriptRuntimeErrorOccurred;


    /// <summary>
    /// Get list of script sets to analyze log.
    /// </summary>
    public IList<LogAnalysisScriptSet> LogAnalysisScriptSets { get => this.logAnalysisScriptSets; }


    /// <inheritdoc/>
    public override long MemorySize 
    {
        get
        {
            var size = Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.analysisResults.Count);
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
        this.updateScriptLogAnalysisAction.Reschedule();
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
                e.NewItems!.Cast<DisplayableLogAnalysisResult>().Let(addedResults =>
                {
                    this.analysisResults.AddAll(e.NewItems!.Cast<DisplayableLogAnalysisResult>());
                    if (addedResults.IsNotEmpty() && !this.GetValue(IsPanelVisibleProperty))
                        this.SetValue(HasNewAnalysisResultsInBackgroundProperty, true);
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogAnalysisResult>().Let(removedResults =>
                {
                    if (removedResults.Count == 1)
                        this.analysisResults.Remove(removedResults[0]);
                    else
                        this.analysisResults.RemoveAll(removedResults);
                });
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
        if (this.analysisResults.IsEmpty())
            this.ResetValue(HasNewAnalysisResultsInBackgroundProperty);
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


    // Called when runtime error occurred by script running by analyzer.
    void OnAnalyzerScriptRuntimeErrorOccurred(object? sender, ScriptRuntimeErrorEventArgs e) =>
        this.LogAnalysisScriptRuntimeErrorOccurred?.Invoke(this, e);


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        // call base
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);

        // reset cooperative log analysis script set
        this.coopScriptLogAnalyzer.ScriptSets.Clear();
        if (newLogProfile?.CooperativeLogAnalysisScriptSet == null)
        {
            if (newLogProfile != null)
                this.Logger.LogDebug("No cooperative log analysis script set found in log profile '{name}' ({id})", newLogProfile.Name, newLogProfile.Id);
            this.ResetValue(HasCooperativeLogAnalysisScriptSetProperty);
            if (this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty))
                this.updateCoopScriptLogAnalysisAction.Execute();
        }
        else
        {
            this.Logger.LogDebug("Cooperative log analysis script set found in log profile '{name}' ({id})", newLogProfile.Name, newLogProfile.Id);
            this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, true);
            if (this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty))
                this.updateCoopScriptLogAnalysisAction.Schedule();
        }

        // reset log analysis rule sets
        if (this.Settings.GetValueOrDefault(SettingKeys.ResetLogAnalysisRuleSetsAfterSettingLogProfile))
        {
            this.keyLogAnalysisRuleSets.Clear();
            this.logAnalysisScriptSets.Clear();
            this.operationCountingAnalysisRuleSets.Clear();
            this.operationDurationAnalysisRuleSets.Clear();
        }
    }


    /// <inheritdoc/>
    protected override void OnLogProfilePropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnLogProfilePropertyChanged(e);
        if (e.PropertyName == nameof(LogProfile.CooperativeLogAnalysisScriptSet))
        {
            var logProfile = this.LogProfile;
            if (logProfile?.CooperativeLogAnalysisScriptSet == null)
            {
                this.Logger.LogDebug("Cooperative log analysis script set of log profile '{name}' ({id}) has been cleared", logProfile?.Name, logProfile?.Id);
                this.ResetValue(HasCooperativeLogAnalysisScriptSetProperty);
                if (this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty))
                    this.updateCoopScriptLogAnalysisAction.Execute();
            }
            else
            {
                if (this.GetValue(HasCooperativeLogAnalysisScriptSetProperty))
                    this.Logger.LogDebug("Cooperative log analysis script set of log profile '{name}' ({id}) has been changed", logProfile.Name, logProfile.Id);
                else
                {
                    this.Logger.LogDebug("Cooperative log analysis script set of log profile '{name}' ({id}) has been set", logProfile.Name, logProfile.Id);
                    this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, true);
                }
                if (this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty))
                    this.updateCoopScriptLogAnalysisAction.Schedule();
            }
        }
    }


    // Called when activation state of product changed.
    void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
    {
        if (productId == Products.Professional && isActivated)
        {
            this.updateCoopScriptLogAnalysisAction.Schedule();
            this.updateScriptLogAnalysisAction.Schedule();
        }
    }


    /// <inheritdoc/>
    protected override void OnRestoreState(JsonElement element)
    {
        // call base
        this.isRestoringState = true;
        base.OnRestoreState(element);

        // restore cooperative log analysis script set state
        if (element.TryGetProperty($"LogAnalysis.{nameof(IsCooperativeLogAnalysisScriptSetEnabled)}", out var jsonValue))
            this.SetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty, jsonValue.ValueKind != JsonValueKind.False);

        // restore rule sets
        this.keyLogAnalysisRuleSets.Clear();
        this.operationCountingAnalysisRuleSets.Clear();
        this.operationDurationAnalysisRuleSets.Clear();
        this.logAnalysisScriptSets.Clear();
        if ((element.TryGetProperty(nameof(KeyLogAnalysisRuleSet), out jsonValue) // for upgrade case
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
        // save cooperative log analysis script set state
        writer.WriteBoolean($"LogAnalysis.{nameof(IsCooperativeLogAnalysisScriptSetEnabled)}", this.GetValue(IsCooperativeLogAnalysisScriptSetEnabledProperty));

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


    /// <inheritdoc/>
    protected override void OnSettingChanged(SettingChangedEventArgs e)
    {
        base.OnSettingChanged(e);
        if (e.Key == AppSuite.SettingKeys.EnableRunningScript)
        {
            if ((bool)e.Value)
                this.updateCoopScriptLogAnalysisAction.Schedule();
            else
            {
                this.logAnalysisScriptSets.Clear();
                this.updateCoopScriptLogAnalysisAction.Execute();
                this.updateScriptLogAnalysisAction.Execute();
            }
        }
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


    /// <inheritdoc/>
    event EventHandler<ScriptRuntimeErrorEventArgs>? IScriptRunningHost.ScriptRuntimeErrorOccurred
    {
        add => this.LogAnalysisScriptRuntimeErrorOccurred += value;
        remove => this.LogAnalysisScriptRuntimeErrorOccurred -= value;
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