using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
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
    public static readonly ObservableProperty<FilterCombinationMode> FiltersCombinationModeProperty = ObservableProperty.Register<LogFilteringViewModel, FilterCombinationMode>(nameof(FiltersCombinationMode), FilterCombinationMode.Intersection);
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


    // Type of token.
    enum TokenType
    {
        HexNumber,
        DecimalNumber,
        VaryingString,
        CjkPhrese,
        Phrase,
        Symbol,
    }


    // Constants.
    const string CjkCharacterPattern = @"(\p{IsCJKRadicalsSupplement}|\p{IsCJKSymbolsandPunctuation}|\p{IsEnclosedCJKLettersandMonths}|\p{IsCJKCompatibility}|\p{IsCJKUnifiedIdeographsExtensionA}|\p{IsCJKUnifiedIdeographs}|\p{IsCJKCompatibilityIdeographs}|\p{IsCJKCompatibilityForms})";
    const string CjkPhrasePattern = $"{CjkCharacterPattern}+";
    const string DecimalNumberPattern = "[0-9]+";
    const string HexNumberPattern = "(0x[a-f0-9]+|[0-9]*[a-f][0-9]+[a-f0-9]+)";
    const string PhrasePattern = @"[\p{L}\p{IsArabic}\p{IsCyrillic}\p{IsDevanagari}\-_]+";


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
    static readonly Regex TokenRegex;


    // Fields.
    readonly MutableObservableBoolean canClearPredefinedTextFilters = new();
    readonly MutableObservableBoolean canFilterBySelectedPid = new();
    readonly MutableObservableBoolean canFilterBySelectedProperty = new();
	readonly MutableObservableBoolean canFilterBySelectedTid = new();
    readonly MutableObservableBoolean canResetFilters = new();
    readonly IDisposable displayLogPropertiesObserverToken;
    readonly DisplayableLogFilter logFilter;
    readonly Stopwatch logFilteringWatch = new();
    readonly ObservableList<PredefinedLogTextFilter> predefinedTextFilters;
    readonly ScheduledAction updateLogFilterAction;


    // Static initializer.
    static LogFilteringViewModel()
    {
        var patternBuffer = new StringBuilder();
        patternBuffer.Append($"(?<{nameof(TokenType.HexNumber)}>{HexNumberPattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.DecimalNumber)}>{DecimalNumberPattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>'[^']*')");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\"[^\"]*\")");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\([^\\)]*\\))");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\[[^\\]]*\\])");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\{{[^\\}}]*\\}})");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\<[^\\>]*\\>)");
        patternBuffer.Append($"|(?<{nameof(TokenType.CjkPhrese)}>{CjkPhrasePattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.Phrase)}>{PhrasePattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.Symbol)}>[^\\s])");
        TokenRegex = new(patternBuffer.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }


    /// <summary>
    /// Initialize new <see cref="LogFilteringViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    public LogFilteringViewModel(Session session) : base(session)
    {
        // start initialization
        var isInit = true; 

        // create command
        this.ClearPredefinedTextFiltersCommand = new Command(() => this.predefinedTextFilters?.Clear(), this.canClearPredefinedTextFilters);
        this.FilterBySelectedProcessIdCommand = new Command<bool>(this.FilterBySelectedProcessId, this.canFilterBySelectedPid);
        this.FilterBySelectedPropertyCommand = new Command<DisplayableLogProperty>(this.FilterBySelectedProperty, this.canFilterBySelectedProperty);
        this.FilterBySelectedThreadIdCommand = new Command<bool>(this.FilterBySelectedThreadId, this.canFilterBySelectedTid);
        this.ResetFiltersCommand = new Command(() => this.ResetFilters(true), this.canResetFilters);

        // create collection
        this.predefinedTextFilters = new ObservableList<PredefinedLogTextFilter>().Also(it =>
        {
            it.CollectionChanged += (_, e) =>
            {
                if (this.IsDisposed)
                    return;
                this.canClearPredefinedTextFilters.Update(it.IsNotEmpty());
                this.updateLogFilterAction?.Reschedule();
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

        // setup properties
        this.SetValue(IgnoreTextFilterCaseProperty, this.Settings.GetValueOrDefault(SettingKeys.IgnoreCaseOfLogTextFilter));

        // create log filter
        this.logFilter = new DisplayableLogFilter(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
        {
            it.PropertyChanged += this.OnLogFilterPropertyChanged;
        });
        this.Logger.LogDebug("Create log filter {logFilter}", this.logFilter);

        // create scheduled action
        this.updateLogFilterAction = new ScheduledAction(() =>
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
            this.TextFilter?.Let(it => 
            {
                var options = it.Options;
                if ((options & RegexOptions.IgnoreCase) == 0)
                {
                    if (this.GetValue(IgnoreTextFilterCaseProperty))
                    {
                        it = new(it.ToString(), options | RegexOptions.IgnoreCase);
                        this.SetValue(TextFilterProperty, it);
                        this.updateLogFilterAction!.Cancel();
                    }
                }
                else
                {
                    if (!this.GetValue(IgnoreTextFilterCaseProperty))
                    {
                        it = new(it.ToString(), options & ~RegexOptions.IgnoreCase);
                        this.SetValue(TextFilterProperty, it);
                        this.updateLogFilterAction!.Cancel();
                    }
                }
                textRegexList.Add(it);
            });
            foreach (var filter in this.predefinedTextFilters)
                textRegexList.Add(filter.Regex);
            this.logFilter.TextRegexList = textRegexList;

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
        });

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
                this.updateLogFilterAction.Schedule();
            }
        });
        this.GetValueAsObservable(IgnoreTextFilterCaseProperty).Subscribe(ignoreCase =>
        {
            if (!isInit)
            {
                if (this.GetValue(TextFilterProperty) != null)
                {
                    this.Logger.LogTrace("Change ignoring case to {ignoreCase} with text filter", ignoreCase);
                    this.updateLogFilterAction.Execute();
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
                this.updateLogFilterAction.Schedule();
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
                this.updateLogFilterAction.Schedule();
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
                this.updateLogFilterAction.Schedule();
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
                this.updateLogFilterAction.Schedule();
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
        this.OnSelectedLogsChanged(null, EventArgs.Empty);
    }


    /// <summary>
    /// Command to clear all predefined log text filters.
    /// </summary>
    public ICommand ClearPredefinedTextFiltersCommand { get; }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // cancel filtering
        this.updateLogFilterAction.Cancel();

        // stop watch
        this.logFilteringWatch.Stop();

        // detach from log filter
        if (this.Application.IsDebugMode)
            this.Logger.LogDebug("Detach from log filter {logFilter}", this.logFilter);
        this.logFilter.PropertyChanged -= this.OnLogFilterPropertyChanged;

        // detach from session
        this.Session.AllLogReadersDisposed -= this.OnAllLogReaderDisposed;
        this.displayLogPropertiesObserverToken.Dispose();
        this.Session.LogSelection.SelectedLogsChanged -= this.OnSelectedLogsChanged;

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
    void FilterBySelectedProperty(DisplayableLogProperty property)
    {
        // check state
        this.VerifyAccess();
        this.VerifyDisposed();

        // get template value
        var propertyValue = this.Session.LogSelection.SelectedLogs.Let(it =>
        {
            if (it.Count != 1)
                return null;
            if (it[0].TryGetProperty<object?>(property.Name, out var value))
                return value?.ToString();
            return null;
        });
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            this.Logger.LogDebug("No property value to filter");
            return;
        }

        // parse template value
        var accuracy = this.Settings.GetValueOrDefault(SettingKeys.AccuracyOfFilteringBySelectedLogProperty);
        var tokens = new List<(TokenType, string)>();
        var tokenTypes = Enum.GetValues<TokenType>();
        while (true)
        {
            var remainingTokenCount = accuracy == Accuracy.High ? 10 : 5;
            var match = TokenRegex.Match(propertyValue);
            while (match.Success && remainingTokenCount > 0)
            {
                var groups = match.Groups;
                foreach (var tokenType in tokenTypes)
                {
                    var name = tokenType.ToString();
                    if (groups[name].Success)
                    {
                        tokens.Add((tokenType, groups[name].Value));
                        if (tokenType != TokenType.Symbol)
                            --remainingTokenCount;
                        break;
                    }
                }
                match = match.NextMatch();
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
                    patternBuffer.Append(@$"\s*{HexNumberPattern}");
                else if (tokenType == TokenType.DecimalNumber)
                    patternBuffer.Append(@$"\s*{DecimalNumberPattern}");
                else if (tokenType == TokenType.VaryingString)
                {
                    var startSymbol = value[0];
                    var endSymbol = value[^1];
                    if (RegexReservedChars.Contains(startSymbol))
                        patternBuffer.Append(@$"\s*\{startSymbol}[^\{endSymbol}]*\{endSymbol}");
                    else
                        patternBuffer.Append(@$"\s*{startSymbol}[^{endSymbol}]*{endSymbol}");
                }
                else if (tokenType == TokenType.CjkPhrese)
                    patternBuffer.Append(@$"\s*{CjkPhrasePattern}");
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
                        patternBuffer.Append(@$"\s+{phrasePattern}");
                    else
                        patternBuffer.Append(@$"\s*{phrasePattern}");
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
        this.SetValue(FiltersCombinationModeProperty, FilterCombinationMode.Intersection);
        this.SetValue(IgnoreTextFilterCaseProperty, true);
        this.ResetFilters(false);
        this.SetValue(TextFilterProperty, new Regex(patternBuffer.ToString(), this.Settings.GetValueOrDefault(SettingKeys.IgnoreCaseOfLogTextFilter) ? RegexOptions.IgnoreCase : RegexOptions.None));
    }


    /// <summary>
    /// Command to use value of property of selected log as text filter.
    /// </summary>
    /// <remarks>The type of parameter is <see cref="DisplayableLogProperty"/> to specify the property.</remarks>
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
        base.OnAllComponentsCreated();
        this.Session.LogSelection.SelectedLogsChanged += this.OnSelectedLogsChanged;
    }


    // Called when all log reader have been disposed.
    void OnAllLogReaderDisposed()
    {
        if (!this.IsDisposed)
            this.SetValue(LastFilteringDurationProperty, null);
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
            this.updateLogFilterAction.Cancel();
            this.ResetValue(IsProcessIdFilterEnabledProperty);
            this.ResetValue(IsThreadIdFilterEnabledProperty);
        }
        else
            this.updateLogFilterAction.Reschedule();
        this.CheckVisibleLogProperties();
        this.ResetFilters();
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
    }


    // Called when selected logs changed.
    void OnSelectedLogsChanged(object? sender, EventArgs e)
    {
        var selectedLogs = this.Session.LogSelection.SelectedLogs;
        var selectionCount = selectedLogs.Count;
        var canFilterByPid = false;
        var canFilterByTid = false;
        var canFilterByProperty = selectionCount == 1;
        if (selectionCount >= 1 && selectionCount <= 64)
        {
            var isPidFilterEnabled = this.GetValue(IsProcessIdFilterEnabledProperty);
            var isTidFilterEnabled = this.GetValue(IsThreadIdFilterEnabledProperty);
            if (isPidFilterEnabled || isTidFilterEnabled)
            {
                canFilterByPid = true;
                canFilterByTid = true;
                var pid = (int?)null;
                var tid = (int?)null;
                for (var i = selectionCount - 1; i >= 0; --i)
                {
                    var log = selectedLogs[i];
                    var localPid = log.ProcessId;
                    var localTid = log.ThreadId;
                    if (localPid != pid)
                    {
                        if (pid.HasValue)
                            canFilterByPid = false;
                        else
                            pid = localPid;
                    }
                    if (localTid != tid)
                    {
                        if (tid.HasValue)
                            canFilterByTid = false;
                        else
                            tid = localTid;
                    }
                }
            }
        }
        this.canFilterBySelectedPid.Update(canFilterByPid);
        this.canFilterBySelectedProperty.Update(canFilterByProperty);
        this.canFilterBySelectedTid.Update(canFilterByTid);
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
    void ResetFilters(bool updateImmediately = true)
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.ResetValue(LevelFilterProperty);
        this.ResetValue(ProcessIdFilterProperty);
        this.ResetValue(ThreadIdFilterProperty);
        this.ResetValue(TextFilterProperty);
        this.predefinedTextFilters.Clear();
        if (updateImmediately)
            this.updateLogFilterAction.Execute();
    }


    /// <summary>
    /// Command to reset all filters.
    /// </summary>
    public ICommand ResetFiltersCommand { get; }


    /// <summary>
    /// Get or set <see cref="Regex"/> for log text filtering.
    /// </summary>
    public Regex? TextFilter
    {
        get => this.GetValue(TextFilterProperty);
        set => this.SetValue(TextFilterProperty, value);
    }


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
        this.canResetFilters.Update(this.GetValue<Logs.LogLevel>(LevelFilterProperty) != Logs.LogLevel.Undefined
            || this.GetValue<int?>(ProcessIdFilterProperty).HasValue
            || this.GetValue<int?>(ThreadIdFilterProperty).HasValue
            || this.GetValue<Regex?>(TextFilterProperty) != null
            || this.predefinedTextFilters.IsNotEmpty()
        );
    }
}