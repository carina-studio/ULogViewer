using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.Text;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log filtering.
/// </summary>
class LogFilteringViewModel : SessionComponent
{
    /// <summary>
    /// Property of <see cref="FilteringProgress"/>.
    /// </summary>
    public static ObservableProperty<double> FilteringProgressProperty = ObservableProperty.Register<LogFilteringViewModel, double>(nameof(FilteringProgress), double.NaN);
    /// <summary>
    /// Property of <see cref="FiltersCombinationMode"/>.
    /// </summary>
    public static readonly ObservableProperty<FilterCombinationMode> FiltersCombinationModeProperty = ObservableProperty.Register<LogFilteringViewModel, FilterCombinationMode>(nameof(FiltersCombinationMode), FilterCombinationMode.Auto);
    /// <summary>
    /// Property of <see cref="IgnoreTextFilterCase"/>.
    /// </summary>
    public static ObservableProperty<bool> IgnoreTextFilterCaseProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IgnoreTextFilterCase), true);
    /// <summary>
    /// Property of <see cref="IsFiltering"/>.
    /// </summary>
    public static ObservableProperty<bool> IsFilteringProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IsFiltering));
    /// <summary>
    /// Property of <see cref="IsFilteringNeeded"/>.
    /// </summary>
    public static ObservableProperty<bool> IsFilteringNeededProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IsFilteringNeeded));
    /// <summary>
    /// Property of <see cref="IsProcessIdFilterEnabled"/>.
    /// </summary>
    public static ObservableProperty<bool> IsProcessIdFilterEnabledProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IsProcessIdFilterEnabled), false);
    /// <summary>
    /// Property of <see cref="IsThreadIdFilterEnabled"/>.
    /// </summary>
    public static ObservableProperty<bool> IsThreadIdFilterEnabledProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IsThreadIdFilterEnabled), false);
    /// <summary>
    /// Property of <see cref="LastFilteringDuration"/>.
    /// </summary>
    public static ObservableProperty<TimeSpan?> LastFilteringDurationProperty = ObservableProperty.Register<LogFilteringViewModel, TimeSpan?>(nameof(LastFilteringDuration));
    /// <summary>
    /// Property of <see cref="LevelFilter"/>.
    /// </summary>
    public static readonly ObservableProperty<Logs.LogLevel> LevelFilterProperty = ObservableProperty.Register<LogFilteringViewModel, Logs.LogLevel>(nameof(LevelFilter), ULogViewer.Logs.LogLevel.Undefined);
    /// <summary>
    /// Property of <see cref="ProcessIdFilter"/>.
    /// </summary>
    public static readonly ObservableProperty<int?> ProcessIdFilterProperty = ObservableProperty.Register<LogFilteringViewModel, int?>(nameof(ProcessIdFilter));
    /// <summary>
    /// Property of <see cref="TextFilter"/>.
    /// </summary>
    public static readonly ObservableProperty<Regex?> TextFilterProperty = ObservableProperty.Register<LogFilteringViewModel, Regex?>(nameof(TextFilter));
    /// <summary>
    /// Property of <see cref="ThreadIdFilter"/>.
    /// </summary>
    public static readonly ObservableProperty<int?> ThreadIdFilterProperty = ObservableProperty.Register<LogFilteringViewModel, int?>(nameof(ThreadIdFilter));


    // Static fields.
    static readonly HashSet<char> RegexReservedChars = new()
    {
        '(', ')',
        '[', ']',
        '{', '}',
        '<', '>',
        '+', '-', '*', '.', ',', '\\',
        '?', '!', '^', '$', '#',
    };
    static readonly HashSet<string> SpecialPhrases = new(StringComparer.Create(CultureInfo.InvariantCulture, true))
    {
        "at",
        "between",
        "by",
        "from",
        "in",
        "of",
        "through",
        "to",
        "via",
    };


    // Fields.
    readonly MutableObservableBoolean canClearPredefinedTextFilters = new();
    readonly MutableObservableBoolean canFilterBySelectedPid = new();
    readonly MutableObservableBoolean canFilterBySelectedProperty = new();
	readonly MutableObservableBoolean canFilterBySelectedTid = new();
    readonly MutableObservableBoolean canResetFilters = new();
    readonly MutableObservableBoolean canUseNextTextFilterOfHistory = new();
    readonly MutableObservableBoolean canUsePreviousTextFilterOfHistory = new();
    readonly ScheduledAction commitFiltersAction;
    readonly IDisposable displayLogPropertiesObserverToken;
    int indexOfTextFilterOnHistory = -1;
    bool isMaxTextFilterHistoryHit;
    readonly DisplayableLogFilter logFilter;
    readonly Stopwatch logFilteringWatch = new();
    readonly ObservableList<PredefinedLogTextFilter> predefinedTextFilters;
    IDisposable? selectedLogStringPropertyValueObserverToken;
    IDisposable? selectedPidObserverToken;
    IDisposable? selectedTidObserverToken;
    readonly ObservableList<string> textFilterHistory = new();


    /// <summary>
    /// Initialize new <see cref="LogFilteringViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogFilteringViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // start initialization
        var isInit = true; 

        // create command
        this.ClearPredefinedTextFiltersCommand = new Command(() => this.predefinedTextFilters?.Clear(), this.canClearPredefinedTextFilters);
        this.FilterBySelectedProcessIdCommand = new Command<bool>(this.FilterBySelectedProcessId, this.canFilterBySelectedPid);
        this.FilterBySelectedPropertyCommand = new Command<Accuracy>(this.FilterBySelectedProperty, this.canFilterBySelectedProperty);
        this.FilterBySelectedThreadIdCommand = new Command<bool>(this.FilterBySelectedThreadId, this.canFilterBySelectedTid);
        this.ResetFiltersCommand = new Command(() => this.ResetFilters(true), this.canResetFilters);
        this.SetFilterCombinationModeCommand = new Command<FilterCombinationMode>(mode => this.FiltersCombinationMode = mode);
        this.UseNextTextFilterOhHistoryCommand = new Command(this.UseNextTextFilterOhHistory, this.canUseNextTextFilterOfHistory);
        this.UsePreviousTextFilterOhHistoryCommand = new Command(this.UsePreviousTextFilterOhHistory, this.canUsePreviousTextFilterOfHistory);

        // create collection
        this.predefinedTextFilters = new ObservableList<PredefinedLogTextFilter>().Also(it =>
        {
            it.CollectionChanged += (_, e) =>
            {
                if (this.IsDisposed)
                    return;
                this.canClearPredefinedTextFilters.Update(it.IsNotEmpty());
                this.commitFiltersAction?.Reschedule();
                if (it.IsNotEmpty())
                {
                    this.Logger.LogTrace("Clear predefined text filters");
                    this.canResetFilters.Update(true);
                }
                else
                {
                    this.Logger.LogTrace("Change predefined text filters: {filterCount}", it.Count);
                    this.UpdateCanResetFilters();
                }
            };
        });
        this.TextFilterHistory = ListExtensions.AsReadOnly(this.textFilterHistory);

        // setup properties
        this.SetValue(IgnoreTextFilterCaseProperty, this.Settings.GetValueOrDefault(SettingKeys.IgnoreCaseOfLogTextFilter));

        // create log filter
        this.logFilter = new DisplayableLogFilter(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
        {
            it.PropertyChanged += this.OnLogFilterPropertyChanged;
        });
        this.Logger.LogDebug("Create log filter {logFilter}", this.logFilter);

        // create scheduled action
        this.commitFiltersAction = new ScheduledAction(this.CommitFilters);

        // attach to configuration
        this.Application.Configuration.SettingChanged += this.OnConfigurationChanged;

        // attach to session
        session.AllLogReadersDisposed += this.OnAllLogReaderDisposed;
        this.displayLogPropertiesObserverToken = session.GetValueAsObservable(Session.DisplayLogPropertiesProperty).Subscribe(properties =>
        {
            if (!isInit)
                this.logFilter.FilteringLogProperties = properties;
        });

        // attach to self properties
        this.GetValueAsObservable(FiltersCombinationModeProperty).Subscribe(mode =>
        {
            if (!isInit)
            {
                this.Logger.LogTrace("Change filters combination mode to {mode}", mode);
                this.commitFiltersAction.Schedule();
            }
        });
        this.GetValueAsObservable(IgnoreTextFilterCaseProperty).Subscribe(ignoreCase =>
        {
            if (!isInit)
            {
                if (this.GetValue(TextFilterProperty) != null)
                {
                    this.Logger.LogTrace("Change ignoring case to {ignoreCase} with text filter", ignoreCase);
                    this.commitFiltersAction.Execute();
                }
                else
                    this.Logger.LogTrace("Change ignoring case to {ignoreCase} without text filter", ignoreCase);
            }
        });
        this.GetValueAsObservable(LevelFilterProperty).Subscribe(level =>
        {
            if (!isInit)
            {
                this.Logger.LogTrace("Change level filter to {level}", level);
                this.commitFiltersAction.Schedule();
                if (level != Logs.LogLevel.Undefined)
                    this.canResetFilters.Update(true);
                else
                    this.UpdateCanResetFilters();
            }
        });
        this.GetValueAsObservable(ProcessIdFilterProperty).Subscribe(pid =>
        {
            if (!isInit)
            {
                if (pid.HasValue)
                    this.Logger.LogTrace("Change PID filter to {pid}", pid);
                else
                    this.Logger.LogTrace("Clear PID filter");
                this.commitFiltersAction.Schedule();
                if (pid.HasValue)
                    this.canResetFilters.Update(true);
                else
                    this.UpdateCanResetFilters();
            }
        });
        this.GetValueAsObservable(TextFilterProperty).Subscribe(pattern =>
        {
            if (!isInit)
            {
                this.commitFiltersAction.Schedule();
                if (pattern != null)
                {
                    this.Logger.LogTrace($"Change text filter");
                    this.canResetFilters.Update(true);
                }
                else
                {
                    this.Logger.LogTrace($"Clear text filter");
                    this.UpdateCanResetFilters();
                }
            }
        });
        this.GetValueAsObservable(ThreadIdFilterProperty).Subscribe(tid =>
        {
            if (!isInit)
            {
                if (tid.HasValue)
                    this.Logger.LogTrace("Change TID filter to {tid}", tid);
                else
                    this.Logger.LogTrace("Clear TID filter");
                this.commitFiltersAction.Schedule();
                if (tid.HasValue)
                    this.canResetFilters.Update(true);
                else
                    this.UpdateCanResetFilters();
            }
        });

        // complete initialization
        isInit = false; 
    }


    // Update state by checking visible log properties.
    void CheckVisibleLogProperties()
    {
        var hasPid = false;
        var hasTid = false;
        this.LogProfile?.VisibleLogProperties?.Let(it =>
        {
            foreach (var property in it)
            {
                if (property.Name == nameof(DisplayableLog.ProcessId))
                    hasPid = true;
                else if (property.Name == nameof(DisplayableLog.ThreadId))
                    hasTid = true;
            }
        });
        if (hasPid)
            this.SetValue(IsProcessIdFilterEnabledProperty, true);
        else
        {
            this.ResetValue(IsProcessIdFilterEnabledProperty);
            this.ResetValue(ProcessIdFilterProperty);
        }
        if (hasTid)
            this.SetValue(IsThreadIdFilterEnabledProperty, true);
        else
        {
            this.ResetValue(IsThreadIdFilterEnabledProperty);
            this.ResetValue(ThreadIdFilterProperty);
        }
    }


    /// <summary>
    /// Command to clear all predefined log text filters.
    /// </summary>
    public ICommand ClearPredefinedTextFiltersCommand { get; }


    // Commit filters to log filter.
    void CommitFilters()
    {
        // check state
        if (this.IsDisposed || this.Session.LogProfile == null)
            return;

        // setup level
        this.logFilter.Level = this.GetValue(LevelFilterProperty);

        // setup PID and TID
        this.logFilter.ProcessId = this.GetValue(IsProcessIdFilterEnabledProperty) 
            ? this.GetValue(ProcessIdFilterProperty) 
            : null;
        this.logFilter.ThreadId = this.GetValue(IsThreadIdFilterEnabledProperty) 
            ? this.GetValue(ThreadIdFilterProperty)
            : null;

        // setup combination mode
        this.logFilter.CombinationMode = this.GetValue(FiltersCombinationModeProperty);

        // setup text regex
        var textRegexList = new List<Regex>();
        var textFilter = this.TextFilter;
        if (textFilter != null)
        {
            var options = textFilter.Options;
            if ((options & RegexOptions.IgnoreCase) == 0)
            {
                if (this.GetValue(IgnoreTextFilterCaseProperty))
                {
                    textFilter = new(textFilter.ToString(), options | RegexOptions.IgnoreCase);
                    this.SetValue(TextFilterProperty, textFilter);
                    this.commitFiltersAction!.Cancel();
                }
            }
            else
            {
                if (!this.GetValue(IgnoreTextFilterCaseProperty))
                {
                    textFilter = new(textFilter.ToString(), options & ~RegexOptions.IgnoreCase);
                    this.SetValue(TextFilterProperty, textFilter);
                    this.commitFiltersAction!.Cancel();
                }
            }
            textRegexList.Add(textFilter);
        }
        foreach (var filter in this.predefinedTextFilters)
            textRegexList.Add(filter.Regex);
        this.logFilter.TextRegexList = textRegexList;
        this.DisplayableLogGroup?.Let(it =>
            it.ActiveTextFilters = textRegexList);

        // print log
        if (this.Application.IsDebugMode)
        {
            this.Logger.LogDebug("Update log filter:");
            this.Logger.LogDebug("  Level: {level}", this.logFilter.Level);
            this.Logger.LogDebug("  PID: {pid}", this.logFilter.ProcessId.Let(pid => pid.HasValue ? pid.ToString() : "Null"));
            this.Logger.LogDebug("  TID: {tid}", this.logFilter.ThreadId.Let(tid => tid.HasValue ? tid.ToString() : "Null"));
            this.Logger.LogDebug("  Text filters: {textRegexListCount}", textRegexList.Count);
        }

        // cancel showing all/marked logs
        if (this.Session.IsShowingAllLogsTemporarily)
            this.Session.ToggleShowingAllLogsTemporarilyCommand.TryExecute();
        if (this.Session.IsShowingMarkedLogsTemporarily)
            this.Session.ToggleShowingMarkedLogsTemporarilyCommand.TryExecute();
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // cancel filtering
        this.commitFiltersAction.Cancel();

        // stop watch
        this.logFilteringWatch.Stop();

        // detach from log filter
        if (this.Application.IsDebugMode)
            this.Logger.LogDebug("Detach from log filter {logFilter}", this.logFilter);
        this.logFilter.PropertyChanged -= this.OnLogFilterPropertyChanged;
        this.logFilter.Dispose();

        // detach from log selection
        this.selectedLogStringPropertyValueObserverToken?.Dispose();
        this.selectedPidObserverToken?.Dispose();
        this.selectedTidObserverToken?.Dispose();

        // detach from session
        this.Session.AllLogReadersDisposed -= this.OnAllLogReaderDisposed;
        this.displayLogPropertiesObserverToken.Dispose();

        // detach from configuration
        this.Application.Configuration.SettingChanged -= this.OnConfigurationChanged;

        // call base
        base.Dispose(disposing);
    }


    // Filter by selected process ID.
    void FilterBySelectedProcessId(bool resetOtherFilters)
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        var pid = this.Session.LogSelection.SelectedLogs.FirstOrDefault()?.ProcessId;
        if (pid == null)
            return;
        if (resetOtherFilters)
            this.ResetFilters(false);
        this.SetValue(ProcessIdFilterProperty, pid);
    }


    /// <summary>
    /// Command to use process ID of selected log as filter.
    /// </summary>
    /// <remarks>The type of parameter is <see cref="bool"/> which indicates whether to reset other filters or not.</remarks>
    public ICommand FilterBySelectedProcessIdCommand { get; }


    // Filter by specific property of selected log.
    void FilterBySelectedProperty(Accuracy accuracy)
    {
        // check state
        this.VerifyAccess();
        this.VerifyDisposed();

        // get template value
        var propertyValue = this.Session.LogSelection.SelectedLogStringPropertyValue;
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            this.Logger.LogDebug("No property value to filter");
            return;
        }

        // parse template value
        var tokens = new List<(TokenType, string)>();
        while (true)
        {
            var remainingTokenCount = accuracy == Accuracy.High ? 10 : 5;
            foreach (var token in new Tokenizer(propertyValue))
            {
                tokens.Add((token.Type, new string(token.Value)));
                if (token.Type != TokenType.Symbol)
                {
                    --remainingTokenCount;
                    if (remainingTokenCount <= 0)
                        break;
                }
            }
            if (tokens.Count == 1 && tokens[0].Item1 == TokenType.VaryingString)
            {
                propertyValue = propertyValue.Trim().Let(it => it[1..^1]);
                tokens.Clear();
                continue;
            }
            break;
        }

        // generate pattern
        var patternBuffer = new StringBuilder();
        var tokenCount = tokens.Count;
        if (tokenCount == 0)
        {
            this.Logger.LogWarning("No token parsed from property value to filter");
            return;
        }
        if (tokenCount == 1)
        {
            (var tokenType, var value) = tokens[0];
            if (tokenType != TokenType.Symbol || !RegexReservedChars.Contains(value[0]))
                patternBuffer.Append(value);
            else
                patternBuffer.Append(@$"\{value}");
        }
        else
        {
            var prevTokenType = default(TokenType);
            var prevToken = "";
            for (var i = 0; i < tokenCount; ++i)
            {
                (var tokenType, var value) = tokens[i];
                if (tokenType == TokenType.HexNumber)
                    patternBuffer.Append(@$"\s*({Tokenizer.HexNumberPattern})");
                else if (tokenType == TokenType.DecimalNumber)
                    patternBuffer.Append(@$"\s*({Tokenizer.DecimalNumberPattern})");
                else if (tokenType == TokenType.VaryingString)
                {
                    var startSymbol = value[0];
                    var endSymbol = value[^1];
                    if (RegexReservedChars.Contains(startSymbol))
                        patternBuffer.Append(@$"\s*(\{startSymbol}[^\{endSymbol}]*\{endSymbol})");
                    else
                        patternBuffer.Append(@$"\s*({startSymbol}[^{endSymbol}]*{endSymbol})");
                }
                else if (tokenType == TokenType.CjkPhrese)
                    patternBuffer.Append(@$"\s*({Tokenizer.CjkPhrasePattern})");
                else if (tokenType == TokenType.Phrase)
                {
                    var phrasePattern = value;
                    if (!SpecialPhrases.Contains(value))
                    {
                        if (accuracy == Accuracy.Low
                            || (i > 0 && prevTokenType == TokenType.Phrase && SpecialPhrases.Contains(prevToken))
                            || (i < tokenCount - 1 && tokens[i + 1].Item1 == TokenType.Phrase && SpecialPhrases.Contains(tokens[i + 1].Item2)))
                        {
                            phrasePattern = @"[\S\D]+";
                        }
                        else
                            phrasePattern = value.Replace("-", "\\-");
                    }
                    if (i > 0 && prevTokenType == TokenType.Phrase)
                        patternBuffer.Append(@$"\s+({phrasePattern})");
                    else
                        patternBuffer.Append(@$"\s*({phrasePattern})");
                }
                else if (tokenType == TokenType.Symbol)
                {
                    var symbol = value[0];
                    if (RegexReservedChars.Contains(symbol))
                        patternBuffer.Append(@$"\s*\{symbol}");
                    else
                        patternBuffer.Append(@$"\s*{symbol}");
                }
                prevTokenType = tokenType;
                prevToken = value;
            }
        }
        var prevTextFilter = this.GetValue(TextFilterProperty);
        var textFilter = new Regex(patternBuffer.ToString(), RegexOptions.IgnoreCase);
        this.SetValue(FiltersCombinationModeProperty, FilterCombinationMode.Intersection);
        this.SetValue(IgnoreTextFilterCaseProperty, true);
        this.ResetFilters(false);
        if (string.IsNullOrEmpty(prevTextFilter?.ToString()))
            this.TextFilter = textFilter; // Use setter to update history of text filter
        else
        {
            this.SetValue(TextFilterProperty, textFilter);
            if (this.textFilterHistory.IsNotEmpty()) // Need to replace empty text filter on top of history queue
            {
                this.textFilterHistory[0] = textFilter.ToString();
                this.UpdateCanUseTextFilterOnHistory();
            }
        }
    }


    /// <summary>
    /// Command to use value of property of selected log as text filter.
    /// </summary>
    /// <remarks>The type of parameter is <see cref="Accuracy"/>.</remarks>
    public ICommand FilterBySelectedPropertyCommand { get; }


    // Filter by selected thread ID.
    void FilterBySelectedThreadId(bool resetOtherFilters)
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        var tid = this.Session.LogSelection.SelectedLogs.FirstOrDefault()?.ThreadId;
        if (tid == null)
            return;
        if (resetOtherFilters)
            this.ResetFilters(false);
        this.SetValue(ThreadIdFilterProperty, tid);
    }


    /// <summary>
    /// Command to use thread ID of selected log as filter.
    /// </summary>
    /// <remarks>The type of parameter is <see cref="bool"/> which indicates whether to reset other filters or not.</remarks>
    public ICommand FilterBySelectedThreadIdCommand { get; }


    /// <summary>
    /// Get list of filtered logs.
    /// </summary>
    public IList<DisplayableLog> FilteredLogs { get => this.logFilter.FilteredLogs; }


    /// <summary>
    /// Get progress of log filterinf.
    /// </summary>
    public double FilteringProgress { get => this.GetValue(FilteringProgressProperty); }


    /// <summary>
    /// Get or set mode to combine condition of <see cref="TextFilter"/> and other conditions for logs filtering.
    /// </summary>
    public FilterCombinationMode FiltersCombinationMode 
    {
        get => this.GetValue(FiltersCombinationModeProperty);
        set => this.SetValue(FiltersCombinationModeProperty, value);
    }


    /// <summary>
    /// Get or set whether case of <see cref="TextFilter"/> should be ignored or not.
    /// </summary>
    public bool IgnoreTextFilterCase
    {
        get => this.GetValue(IgnoreTextFilterCaseProperty);
        set => this.SetValue(IgnoreTextFilterCaseProperty, value);
    }


    /// <summary>
    /// Notify that given log was updated and should be processed again.
    /// </summary>
    /// <param name="log">Log to be processed again.</param>
    public void InvalidateLog(DisplayableLog log) =>
        this.logFilter.InvalidateLog(log);


    /// <summary>
    /// Check whether there is on-going log filtering or not.
    /// </summary>
    public bool IsFiltering { get => this.GetValue(IsFilteringProperty); }


    /// <summary>
    /// Check whether log filtering is needed or not.
    /// </summary>
    public bool IsFilteringNeeded { get => this.GetValue(IsFilteringNeededProperty); }


    /// <summary>
    /// Check whether <see cref="ProcessIdFilter"/> is valid to filter logs in current log profile or not.
    /// </summary>
    public bool IsProcessIdFilterEnabled { get => this.GetValue(IsProcessIdFilterEnabledProperty); }


    /// <summary>
    /// Check whether <see cref="ThreadIdFilter"/> is valid to filter logs in current log profile or not.
    /// </summary>
    public bool IsThreadIdFilterEnabled { get => this.GetValue(IsThreadIdFilterEnabledProperty); }


    /// <summary>
    /// Get duration of last log filtering.
    /// </summary>
    public TimeSpan? LastFilteringDuration { get => this.GetValue(LastFilteringDurationProperty); }


    /// <summary>
    /// Get or set level to filter logs.
    /// </summary>
    public Logs.LogLevel LevelFilter 
    { 
        get => this.GetValue(LevelFilterProperty);
        set => this.SetValue(LevelFilterProperty, value);
    }


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize + this.logFilter.MemorySize;


    /// <inheritdoc/>
    protected override void OnAllComponentsCreated()
    {
        // call base
        base.OnAllComponentsCreated();

        // attach to log selection
        this.Session.LogSelection.Let(it =>
        {
            this.selectedLogStringPropertyValueObserverToken = it.GetValueAsObservable(LogSelectionViewModel.SelectedLogStringPropertyValueProperty).Subscribe(value =>
            {
                this.canFilterBySelectedProperty.Update(!string.IsNullOrWhiteSpace(value));
            });
            this.selectedPidObserverToken = it.GetValueAsObservable(LogSelectionViewModel.SelectedProcessIdProperty).Subscribe(pid =>
            {
                this.canFilterBySelectedPid.Update(pid.HasValue);
            });
            this.selectedTidObserverToken = it.GetValueAsObservable(LogSelectionViewModel.SelectedThreadIdProperty).Subscribe(tid =>
            {
                this.canFilterBySelectedTid.Update(tid.HasValue);
            });
        });
    }


    // Called when all log reader have been disposed.
    void OnAllLogReaderDisposed()
    {
        if (!this.IsDisposed)
            this.SetValue(LastFilteringDurationProperty, null);
    }


    // Called when configuration changed.
    void OnConfigurationChanged(object? sender, SettingChangedEventArgs e)
    {
        if (e.Key == ConfigurationKeys.LogTextFilterHistoryCount)
        {
            var newCount = Math.Max(1, (int)e.Value);
            if (this.textFilterHistory.Count >= newCount)
            {
                this.isMaxTextFilterHistoryHit = true;
                this.textFilterHistory.RemoveRange(newCount, this.textFilterHistory.Count - newCount);
            }
        }
    }


    /// <inheritdoc/>
    protected override void OnDisplayableLogGroupCreated()
    {
        base.OnDisplayableLogGroupCreated();
        this.DisplayableLogGroup?.Let(it =>
        {
            it.ActiveTextFilters = this.logFilter.TextRegexList;
        });
    }


    // Called when property of log filter changed.
    void OnLogFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DisplayableLogFilter.IsProcessing):
                if(this.logFilter.IsProcessing)
                {
                    if (!this.logFilteringWatch.IsRunning)
                        this.logFilteringWatch.Restart();
                    this.SetValue(IsFilteringProperty, true);
                }
                else
                {
                    this.logFilteringWatch.Stop();
                    this.SetValue(IsFilteringProperty, false);
                    this.SetValue(LastFilteringDurationProperty, TimeSpan.FromMilliseconds(this.logFilteringWatch.ElapsedMilliseconds));
                }
                break;
            case nameof(DisplayableLogFilter.IsProcessingNeeded):
                this.SetValue(IsFilteringNeededProperty, this.logFilter.IsProcessingNeeded);
                break;
            case nameof(DisplayableLogFilter.Progress):
                this.SetValue(FilteringProgressProperty, this.logFilter.Progress);
                break;
        }
    }


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);
        if (newLogProfile == null)
        {
            this.logFilter.FilteringLogProperties = Session.DisplayLogPropertiesProperty.DefaultValue;
            this.commitFiltersAction.Cancel();
            this.ResetValue(IsProcessIdFilterEnabledProperty);
            this.ResetValue(IsThreadIdFilterEnabledProperty);
        }
        else
            this.commitFiltersAction.Reschedule();
        this.CheckVisibleLogProperties();
        this.ResetFilters(true);
    }


    /// <inheritdoc/>
    protected override void OnLogProfilePropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnLogProfilePropertyChanged(e);
        if (e.PropertyName == nameof(LogProfile.VisibleLogProperties))
            this.CheckVisibleLogProperties();
    }


    /// <inheritdoc/>
    protected override void OnRestoreState(JsonElement element)
    {
        // call base
        base.OnRestoreState(element);

        // restore filtering parameters
        if ((element.TryGetProperty("LogFiltersCombinationMode", out var jsonValue) // Upgrade case
            || element.TryGetProperty($"LogFiltering.{nameof(FiltersCombinationMode)}", out jsonValue))
                && jsonValue.ValueKind == JsonValueKind.String
                && Enum.TryParse<FilterCombinationMode>(jsonValue.GetString(), out var combinationMode))
        {
            this.SetValue(FiltersCombinationModeProperty, combinationMode);
        }
        if (element.TryGetProperty($"LogFiltering.{nameof(IgnoreTextFilterCase)}", out jsonValue))
            this.SetValue(IgnoreTextFilterCaseProperty, jsonValue.ValueKind != JsonValueKind.False);
        if ((element.TryGetProperty("LogLevelFilter", out jsonValue) // Upgrade case
            || element.TryGetProperty($"LogFiltering.{nameof(LevelFilter)}", out jsonValue))
                && jsonValue.ValueKind == JsonValueKind.String
                && Enum.TryParse<Logs.LogLevel>(jsonValue.GetString(), out var level))
        {
            this.SetValue(LevelFilterProperty, level);
        }
        if ((element.TryGetProperty("LogProcessIdFilter", out jsonValue) // Upgrade case
            || element.TryGetProperty($"LogFiltering.{nameof(ProcessIdFilter)}", out jsonValue))
                && jsonValue.TryGetInt32(out var pid))
        {
            this.SetValue(ProcessIdFilterProperty, pid);
        }
        if ((element.TryGetProperty("LogTextFilter", out jsonValue) // Upgrade case
            || element.TryGetProperty($"LogFiltering.{nameof(TextFilter)}", out jsonValue))
                && jsonValue.ValueKind == JsonValueKind.Object
                && jsonValue.TryGetProperty("Pattern", out var jsonPattern)
                && jsonPattern.ValueKind == JsonValueKind.String)
        {
            var options = RegexOptions.None;
            jsonValue.TryGetProperty("Options", out var jsonOptions);
            if (jsonOptions.TryGetInt32(out var optionsValue))
                options = (RegexOptions)optionsValue;
            try
            {
                this.SetValue(TextFilterProperty, new Regex(jsonPattern.GetString().AsNonNull(), options));
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Unable to restore log text filter");
            }
        }
        if ((element.TryGetProperty("LogThreadIdFilter", out jsonValue) // Upgrade case
            || element.TryGetProperty($"LogFiltering.{nameof(ThreadIdFilter)}", out jsonValue))
                && jsonValue.TryGetInt32(out var tid))
        {
            this.SetValue(ThreadIdFilterProperty, tid);
        }
        if ((element.TryGetProperty("PredefinedLogTextFilters", out jsonValue) // Upgrade case
            || element.TryGetProperty($"LogFiltering.{nameof(PredefinedTextFilters)}", out jsonValue))
                && jsonValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var jsonId in jsonValue.EnumerateArray())
            {
                jsonId.GetString()?.Let(id =>
                {
                    PredefinedLogTextFilterManager.Default.GetFilterOrDefault(id)?.Let(filter =>
                        this.predefinedTextFilters.Add(filter));
                });
            }
        }
        if (element.TryGetProperty($"LogFiltering.{nameof(TextFilterHistory)}", out jsonValue)
            && jsonValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var jsonFilter in jsonValue.EnumerateArray())
                this.textFilterHistory.Add(jsonFilter.GetString() ?? "");
        }
        if (element.TryGetProperty("LogFiltering.IsMaxTextFilterHistoryHit", out jsonValue)
            && jsonValue.ValueKind == JsonValueKind.True)
        {
            this.isMaxTextFilterHistoryHit = true;
        }
        if (element.TryGetProperty("LogFiltering.IndexOfTextFilterOnHistory", out jsonValue)
            && jsonValue.ValueKind == JsonValueKind.Number
            && jsonValue.TryGetInt32(out var index))
        {
            this.indexOfTextFilterOnHistory = index;
        }
        this.UpdateCanUseTextFilterOnHistory();
    }


    /// <inheritdoc/>
    protected override void OnSaveState(Utf8JsonWriter writer)
    {
        // call base
        base.OnSaveState(writer);

        // save filtering parameters
        writer.WriteString($"LogFiltering.{nameof(FiltersCombinationMode)}", this.FiltersCombinationMode.ToString());
        if (!this.GetValue(IgnoreTextFilterCaseProperty))
            writer.WriteBoolean($"LogFiltering.{nameof(IgnoreTextFilterCase)}", false);
        writer.WriteString($"LogFiltering.{nameof(LevelFilter)}", this.LevelFilter.ToString());
        this.GetValue(ProcessIdFilterProperty)?.Let(it => writer.WriteNumber($"LogFiltering.{nameof(ProcessIdFilter)}", it));
        this.GetValue(TextFilterProperty)?.Let(it =>
        {
            writer.WritePropertyName($"LogFiltering.{nameof(TextFilter)}");
            writer.WriteStartObject();
            writer.WriteString("Pattern", it.ToString());
            writer.WriteNumber("Options", (int)it.Options);
            writer.WriteEndObject();
        });
        this.GetValue(ThreadIdFilterProperty)?.Let(it => writer.WriteNumber($"LogFiltering.{nameof(ThreadIdFilter)}", it));
        if (this.predefinedTextFilters.IsNotEmpty())
        {
            writer.WritePropertyName($"LogFiltering.{nameof(PredefinedTextFilters)}");
            writer.WriteStartArray();
            foreach (var filter in this.predefinedTextFilters)
                writer.WriteStringValue(filter.Id);
            writer.WriteEndArray();
        }
        if (this.textFilterHistory.IsNotEmpty())
        {
            writer.WritePropertyName($"LogFiltering.{nameof(TextFilterHistory)}");
            writer.WriteStartArray();
            foreach (var filter in this.textFilterHistory)
                writer.WriteStringValue(filter);
            writer.WriteEndArray();
        }
        if (this.isMaxTextFilterHistoryHit)
            writer.WriteBoolean("LogFiltering.IsMaxTextFilterHistoryHit", true);
        if (this.indexOfTextFilterOnHistory >= 0)
            writer.WriteNumber("LogFiltering.IndexOfTextFilterOnHistory", this.indexOfTextFilterOnHistory);
    }


    /// <summary>
    /// Get list of <see cref="PredefinedLogTextFilter"/>s to filter logs.
    /// </summary>
    public IList<PredefinedLogTextFilter> PredefinedTextFilters { get => this.predefinedTextFilters; }


    /// <summary>
    /// Get or set process ID to filter logs.
    /// </summary>
    public int? ProcessIdFilter
    {
        get => this.GetValue(ProcessIdFilterProperty);
        set => this.SetValue(ProcessIdFilterProperty, value);
    }


    // Reset all filters.
    void ResetFilters(bool updateImmediately)
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.ResetValue(LevelFilterProperty);
        this.ResetValue(ProcessIdFilterProperty);
        this.ResetValue(ThreadIdFilterProperty);
        this.TextFilter = null; // Need to use setter to update history of text filter
        this.predefinedTextFilters.Clear();
        if (updateImmediately)
            this.commitFiltersAction.Execute();
    }


    /// <summary>
    /// Command to reset all filters.
    /// </summary>
    public ICommand ResetFiltersCommand { get; }


    /// <summary>
    /// Command to set <see cref="LogFilteringViewModel.FilterCombinationMode"/>.
    /// </summary>
    /// <value>The type of parameter is <see cref="FilterCombinationMode"/>.</value>
    public ICommand SetFilterCombinationModeCommand { get; }


    /// <summary>
    /// Get or set <see cref="Regex"/> for log text filtering.
    /// </summary>
    public Regex? TextFilter
    {
        get => this.GetValue(TextFilterProperty);
        set 
        {
            // get current filter
            var prevPattern = this.GetValue(TextFilterProperty)?.ToString();

            // update filter
            this.SetValue(TextFilterProperty, value);

            // update filter history
            var pattern = value?.ToString();
            if (prevPattern != pattern)
            {
                if (this.indexOfTextFilterOnHistory > 0)
                    this.textFilterHistory.RemoveRange(0, this.indexOfTextFilterOnHistory);
                if (this.textFilterHistory.IsEmpty() || this.textFilterHistory[0] != pattern)
                {
                    var historySize = Math.Max(1, this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogTextFilterHistoryCount));
                    if (this.textFilterHistory.Count >= historySize)
                    {
                        this.isMaxTextFilterHistoryHit = true;
                        this.textFilterHistory.RemoveRange(historySize - 1, this.textFilterHistory.Count - historySize + 1);
                    }
                    this.textFilterHistory.Insert(0, pattern ?? "");
                }
                this.indexOfTextFilterOnHistory = 0;
                this.UpdateCanUseTextFilterOnHistory();
            }
        }
    }


    /// <summary>
    /// Histroy of applied <see cref="TextFilter"/>.
    /// </summary>
    public IList<string> TextFilterHistory { get; }


    /// <summary>
    /// Get or set thread ID to filter logs.
    /// </summary>
    public int? ThreadIdFilter
    {
        get => this.GetValue(ThreadIdFilterProperty);
        set => this.SetValue(ThreadIdFilterProperty, value);
    }


    // Update can reset filters state.
    void UpdateCanResetFilters()
    {
        this.canResetFilters.Update(this.GetValue(LevelFilterProperty) != Logs.LogLevel.Undefined
            || this.GetValue(ProcessIdFilterProperty).HasValue
            || this.GetValue(ThreadIdFilterProperty).HasValue
            || this.GetValue(TextFilterProperty) != null
            || this.predefinedTextFilters.IsNotEmpty()
        );
    }


    // Update can use text filter of history.
    void UpdateCanUseTextFilterOnHistory()
    {
        if (this.textFilterHistory.IsEmpty())
        {
            this.canUseNextTextFilterOfHistory.Update(false);
            this.canUsePreviousTextFilterOfHistory.Update(false);
            this.indexOfTextFilterOnHistory = -1;
            return;
        }
        this.canUseNextTextFilterOfHistory.Update(this.indexOfTextFilterOnHistory > 0);
        if (this.isMaxTextFilterHistoryHit)
            this.canUsePreviousTextFilterOfHistory.Update(this.indexOfTextFilterOnHistory < this.textFilterHistory.Count - 1);
        else
            this.canUsePreviousTextFilterOfHistory.Update(this.indexOfTextFilterOnHistory < this.textFilterHistory.Count);
    }


    // Use next text filter of history.
    void UseNextTextFilterOhHistory()
    {
        --this.indexOfTextFilterOnHistory;
        if (this.indexOfTextFilterOnHistory < 0)
        {
            this.indexOfTextFilterOnHistory = 0;
            this.UpdateCanUseTextFilterOnHistory();
            return;
        }
        this.UpdateCanUseTextFilterOnHistory();
        var pattern = this.textFilterHistory[this.indexOfTextFilterOnHistory];
        if (!string.IsNullOrEmpty(pattern))
        {
            var options = this.GetValue(IgnoreTextFilterCaseProperty) ? RegexOptions.IgnoreCase : RegexOptions.None;
            this.SetValue(TextFilterProperty, new(pattern, options));
        }
        else
            this.ResetValue(TextFilterProperty);
    }


    /// <summary>
    /// Command to use next text filter of history.
    /// </summary>
    public ICommand UseNextTextFilterOhHistoryCommand { get; }


    // Use previous text filter of history.
    void UsePreviousTextFilterOhHistory()
    {
        ++this.indexOfTextFilterOnHistory;
        if (this.indexOfTextFilterOnHistory >= this.textFilterHistory.Count)
        {
            if (this.isMaxTextFilterHistoryHit)
                --this.indexOfTextFilterOnHistory;
            else
            {
                this.indexOfTextFilterOnHistory = this.textFilterHistory.Count;
                this.SetValue(TextFilterProperty, null);
            }
            this.UpdateCanUseTextFilterOnHistory();
            return;
        }
        this.UpdateCanUseTextFilterOnHistory();
        var pattern = this.textFilterHistory[this.indexOfTextFilterOnHistory];
        if (!string.IsNullOrEmpty(pattern))
        {
            var options = this.GetValue(IgnoreTextFilterCaseProperty) ? RegexOptions.IgnoreCase : RegexOptions.None;
            this.SetValue(TextFilterProperty, new(pattern, options));
        }
        else
            this.ResetValue(TextFilterProperty);
    }


    /// <summary>
    /// Command to use previous text filter of history.
    /// </summary>
    public ICommand UsePreviousTextFilterOhHistoryCommand { get; }
}