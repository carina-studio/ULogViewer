using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    /// Property of <see cref="IsFiltering"/>.
    /// </summary>
    public static ObservableProperty<bool> IsFilteringProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IsFiltering));
    /// <summary>
    /// Property of <see cref="IsFilteringNeeded"/>.
    /// </summary>
    public static ObservableProperty<bool> IsFilteringNeededProperty = ObservableProperty.Register<LogFilteringViewModel, bool>(nameof(IsFilteringNeeded));
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


    // Fields.
    readonly MutableObservableBoolean canClearPredefinedTextFilters = new();
    readonly MutableObservableBoolean canResetFilters = new();
    readonly IDisposable displayLogPropertiesObserverToken;
    readonly DisplayableLogFilter logFilter;
    readonly Stopwatch logFilteringWatch = new Stopwatch();
    readonly ObservableList<PredefinedLogTextFilter> predefinedTextFilters;
    readonly ScheduledAction updateLogFilterAction;


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
        this.ResetFiltersCommand = new Command(this.ResetFilters, this.canResetFilters);

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
                    this.canResetFilters.Update(true);
                else
                    this.UpdateCanResetFilters();
            };
        });

        // create log filter
        this.logFilter = new DisplayableLogFilter(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
        {
            it.PropertyChanged += this.OnLogFilterPropertyChanged;
        });

        // create scheduled action
        this.updateLogFilterAction = new ScheduledAction(() =>
        {
            // check state
            if (this.IsDisposed || this.Session.LogProfile == null)
                return;

            // setup level
            this.logFilter.Level = this.GetValue(LevelFilterProperty);

            // setup PID and TID
            this.logFilter.ProcessId = this.GetValue(ProcessIdFilterProperty);
            this.logFilter.ThreadId = this.GetValue(ThreadIdFilterProperty);

            // setup combination mode
            this.logFilter.CombinationMode = this.GetValue(FiltersCombinationModeProperty);

            // setup text regex
            List<Regex> textRegexList = new List<Regex>();
            this.TextFilter?.Let(it => textRegexList.Add(it));
            foreach (var filter in this.predefinedTextFilters)
                textRegexList.Add(filter.Regex);
            this.logFilter.TextRegexList = textRegexList;

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
        this.GetValueAsObservable(FiltersCombinationModeProperty).Subscribe(_ =>
        {
            if (!isInit)
                this.updateLogFilterAction.Schedule();
        });
        this.GetValueAsObservable(LevelFilterProperty).Subscribe(level =>
        {
            if (!isInit)
            {
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
                    this.canResetFilters.Update(true);
                else
                    this.UpdateCanResetFilters();
            }
        });
        this.GetValueAsObservable(ThreadIdFilterProperty).Subscribe(tid =>
        {
            if (!isInit)
            {
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
        this.logFilter.PropertyChanged -= this.OnLogFilterPropertyChanged;

        // detach from session
        this.Session.AllLogReadersDisposed -= this.OnAllLogReaderDisposed;
        this.displayLogPropertiesObserverToken.Dispose();

        // call base
        base.Dispose(disposing);
    }


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
        }
        else
            this.updateLogFilterAction.Reschedule();
        this.ResetFilters();
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
    void ResetFilters()
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.SetValue<Logs.LogLevel>(LevelFilterProperty, Logs.LogLevel.Undefined);
        this.SetValue<int?>(ProcessIdFilterProperty, null);
        this.SetValue<int?>(ThreadIdFilterProperty, null);
        this.SetValue<Regex?>(TextFilterProperty, null);
        this.predefinedTextFilters.Clear();
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