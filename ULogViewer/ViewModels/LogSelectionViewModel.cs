using CarinaStudio.Collections;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log selection.
/// </summary>
class LogSelectionViewModel : SessionComponent
{
    /// <summary>
    /// Property of <see cref="EarliestSelectedLogTimestamp"/>.
    /// </summary>
    public static readonly ObservableProperty<DateTime?> EarliestSelectedLogTimestampProperty = ObservableProperty.Register<LogSelectionViewModel, DateTime?>(nameof(EarliestSelectedLogTimestamp));
    /// <summary>
    /// Property of <see cref="HasSelectedLogs"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> HasSelectedLogsProperty = ObservableProperty.Register<LogSelectionViewModel, bool>(nameof(HasSelectedLogs));
    /// <summary>
    /// Property of <see cref="IsAllLogsSelectionRequested"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsAllLogsSelectionRequestedProperty = ObservableProperty.Register<LogSelectionViewModel, bool>(nameof(IsAllLogsSelectionRequested));
    /// <summary>
    /// Property of <see cref="LatestSelectedLogTimestamp"/>.
    /// </summary>
    public static readonly ObservableProperty<DateTime?> LatestSelectedLogTimestampProperty = ObservableProperty.Register<LogSelectionViewModel, DateTime?>(nameof(LatestSelectedLogTimestamp));
    /// <summary>
    /// Property of <see cref="SelectedLogProperty"/>.
    /// </summary>
    public static readonly ObservableProperty<DisplayableLogProperty?> SelectedLogPropertyProperty = ObservableProperty.Register<LogSelectionViewModel, DisplayableLogProperty?>(nameof(SelectedLogProperty), coerce: (vm, p) =>
    {
        if (p == null)
            return null;
        return vm.Session.DisplayLogProperties.FirstOrDefault(it => it == p);
    });
    /// <summary>
    /// Property of <see cref="SelectedLogPropertyValue"/>.
    /// </summary>
    public static readonly ObservableProperty<object?> SelectedLogPropertyValueProperty = ObservableProperty.Register<LogSelectionViewModel, object?>(nameof(SelectedLogPropertyValue));
    /// <summary>
    /// Property of <see cref="SelectedLogsDuration"/>.
    /// </summary>
    public static readonly ObservableProperty<TimeSpan?> SelectedLogsDurationProperty = ObservableProperty.Register<LogSelectionViewModel, TimeSpan?>(nameof(SelectedLogsDuration));
    /// <summary>
    /// Property of <see cref="SelectedLogStringPropertyValue"/>.
    /// </summary>
    public static readonly ObservableProperty<string?> SelectedLogStringPropertyValueProperty = ObservableProperty.Register<LogSelectionViewModel, string?>(nameof(SelectedLogStringPropertyValue));
    /// <summary>
    /// Property of <see cref="SelectedProcessId"/>.
    /// </summary>
    public static readonly ObservableProperty<int?> SelectedProcessIdProperty = ObservableProperty.Register<LogSelectionViewModel, int?>(nameof(SelectedProcessId));
    /// <summary>
    /// Property of <see cref="SelectedThreadId"/>.
    /// </summary>
    public static readonly ObservableProperty<int?> SelectedThreadIdProperty = ObservableProperty.Register<LogSelectionViewModel, int?>(nameof(SelectedThreadId));


    // Constant.
    const int SelectedLogsTimeInfoReportingDelay = 200;


    // Fields.
    INotifyCollectionChanged? attachedLogs;
    readonly MutableObservableBoolean canSelectLogsDurationEndingLog = new();
    readonly MutableObservableBoolean canSelectLogsDurationStartingLog = new();
    readonly IDisposable displayableLogPropertiesObserverToken;
    readonly IDisposable earliestLogTimestampObserverToken;
    readonly IDisposable latestLogTimestampObserverToken;
    readonly IDisposable logsObserverToken;
    readonly IDisposable maxLogTimeSpanObserverToken;
    readonly IDisposable minLogTimeSpanObserverToken;
    readonly ScheduledAction notifySelectedLogsChangedAction;
    readonly ScheduledAction reportSelectedLogsTimeInfoAction;
    readonly SortedObservableList<DisplayableLog> selectedLogs;
    readonly ScheduledAction updateCanSelectLogsDurationStartingEndingLogsAction;


    /// <summary>
    /// Initialize new <see cref="LogSelectionViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogSelectionViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // create command
        var hasSelectedLogsObservable = this.GetValueAsObservable(HasSelectedLogsProperty);
        this.ClearSelectedLogsCommand = new Command(this.ClearSelectedLogs, hasSelectedLogsObservable);
        this.CopySelectedLogsCommand = new Command(this.CopySelectedLogs, hasSelectedLogsObservable);
        this.CopySelectedLogsWithFileNamesCommand = new Command(this.CopySelectedLogsWithFileNames, new ForwardedObservableBoolean(ForwardedObservableBoolean.CombinationMode.And,
            false, 
            hasSelectedLogsObservable, 
            session.GetValueAsObservable(Session.AreFileBasedLogsProperty)));
        this.SelectAllLogsCommand = new Command(this.SelectAllLogs, session.GetValueAsObservable(Session.HasLogsProperty));
        this.SelectLogDurationEndingLogCommand = new Command(this.SelectLogDurationEndingLog, this.canSelectLogsDurationEndingLog);
        this.SelectLogDurationStartingLogCommand = new Command(this.SelectLogDurationStartingLog, this.canSelectLogsDurationStartingLog);
        this.SelectMarkedLogsCommand = new Command(this.SelectMarkedLogs, session.GetValueAsObservable(Session.HasMarkedLogsProperty));

        // create collection
        this.selectedLogs = new SortedObservableList<DisplayableLog>(this.CompareLogs).Also(it =>
        {
            it.CollectionChanged += this.OnSelectedLogsChanged;
        });

        // create actions
        this.notifySelectedLogsChangedAction = new(() =>
        {
            if (!this.IsDisposed)
			    this.SelectedLogsChanged?.Invoke(this, EventArgs.Empty);
        });
        this.reportSelectedLogsTimeInfoAction = new ScheduledAction(() =>
        {
            if (this.IsDisposed)
                return;
            var firstLog = this.selectedLogs.FirstOrDefault();
            var lastLog = this.selectedLogs.LastOrDefault();
            if (firstLog == null || lastLog == null || firstLog == lastLog)
            {
                this.ResetValue(SelectedLogsDurationProperty);
                this.ResetValue(EarliestSelectedLogTimestampProperty);
                this.ResetValue(LatestSelectedLogTimestampProperty);
                return;
            }
            var earliestTimestamp = (DateTime?)null;
            var latestTimestamp = (DateTime?)null;
            var minTimeSpan = (TimeSpan?)null;
            var maxTimeSpan = (TimeSpan?)null;
            var duration = Session.CalculateDurationBetweenLogs(firstLog, lastLog, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);
            if (duration.HasValue)
            {
                this.SetValue(SelectedLogsDurationProperty, duration);
                this.SetValue(EarliestSelectedLogTimestampProperty, earliestTimestamp);
                this.SetValue(LatestSelectedLogTimestampProperty, latestTimestamp);
            }
            else
            {
                this.ResetValue(SelectedLogsDurationProperty);
                this.ResetValue(EarliestSelectedLogTimestampProperty);
                this.ResetValue(LatestSelectedLogTimestampProperty);
            }
        });
        this.updateCanSelectLogsDurationStartingEndingLogsAction = new(() =>
        {
            this.canSelectLogsDurationEndingLog.Update(this.Session.LatestLogTimestamp.HasValue || this.Session.MaxLogTimeSpan.HasValue);
            this.canSelectLogsDurationStartingLog.Update(this.Session.EarliestLogTimestamp.HasValue || this.Session.MinLogTimeSpan.HasValue);
        });
        
        // attach to session
        this.displayableLogPropertiesObserverToken = session.GetValueAsObservable(Session.DisplayLogPropertiesProperty).Subscribe(properties =>
        {
            var selectedProperty = this.GetValue(SelectedLogPropertyProperty);
            if (selectedProperty != null && !properties.Contains(selectedProperty))
                this.ResetValue(SelectedLogPropertyProperty);
        });
        this.earliestLogTimestampObserverToken = session.GetValueAsObservable(Session.EarliestLogTimestampProperty).Subscribe(_ =>
            this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule());
        this.latestLogTimestampObserverToken = session.GetValueAsObservable(Session.LatestLogTimestampProperty).Subscribe(_ =>
            this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule());
        this.logsObserverToken = session.GetValueAsObservable(Session.LogsProperty).Subscribe(logs =>
        {
            if (this.attachedLogs != null)
                this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
            this.ClearSelectedLogs();
            this.attachedLogs = logs as INotifyCollectionChanged;
            if (this.attachedLogs != null)
                this.attachedLogs.CollectionChanged += this.OnLogsChanged;
        });
        this.maxLogTimeSpanObserverToken = session.GetValueAsObservable(Session.MaxLogTimeSpanProperty).Subscribe(_ =>
            this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule());
        this.minLogTimeSpanObserverToken = session.GetValueAsObservable(Session.MinLogTimeSpanProperty).Subscribe(_ =>
            this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule());
        
        // attach to self
        this.GetValueAsObservable(SelectedLogPropertyProperty).Subscribe(this.ReportSelectedLogPropertyValue);
    }


    /// <summary>
    /// Clear all selected logs.
    /// </summary>
    public void ClearSelectedLogs()
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.SetValue(IsAllLogsSelectionRequestedProperty, false);
        this.selectedLogs.Clear();
    }


    /// <summary>
    /// Copy selected logs.
    /// </summary>
    public void CopySelectedLogs() =>
        this.Session.CopyLogsCommand.TryExecute(this.selectedLogs);


    /// <summary>
    /// Command to copy selected logs.
    /// </summary>
    public ICommand CopySelectedLogsCommand { get; }


    /// <summary>
    /// Copy selected logs with file names.
    /// </summary>
    public void CopySelectedLogsWithFileNames() =>
        this.Session.CopyLogsWithFileNamesCommand.TryExecute(this.selectedLogs);


    /// <summary>
    /// Command to copy selected logs with file names.
    /// </summary>
    public ICommand CopySelectedLogsWithFileNamesCommand { get; }


    /// <summary>
    /// Command to clear all selected logs.
    /// </summary>
    public ICommand ClearSelectedLogsCommand { get; }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // detach from session
        if (this.attachedLogs != null)
        {
            this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
            this.attachedLogs = null;
        }
        this.displayableLogPropertiesObserverToken.Dispose();
        this.earliestLogTimestampObserverToken.Dispose();
        this.latestLogTimestampObserverToken.Dispose();
        this.logsObserverToken.Dispose();
        this.maxLogTimeSpanObserverToken.Dispose();
        this.minLogTimeSpanObserverToken.Dispose();

        // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Get earliest timestamp of selected log.
    /// </summary>
	public DateTime? EarliestSelectedLogTimestamp { get => this.GetValue(EarliestSelectedLogTimestampProperty); }


    /// <summary>
    /// Check whether at least one log has been selected or not.
    /// </summary>
    public bool HasSelectedLogs { get => this.GetValue(HasSelectedLogsProperty); }


    /// <summary>
    /// Check whether selecting all logs has been requested or not.
    /// </summary>
    public bool IsAllLogsSelectionRequested { get => this.GetValue(IsAllLogsSelectionRequestedProperty); }


    /// <summary>
    /// Get latest timestamp of selected log.
    /// </summary>
	public DateTime? LatestSelectedLogTimestamp { get => this.GetValue(LatestSelectedLogTimestampProperty); }


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize
        + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.selectedLogs.Count);
    

    /// <inheritdoc/>
    protected override void OnDisplayableLogGroupCreated()
    {
        base.OnDisplayableLogGroupCreated();
        this.DisplayableLogGroup?.Let(it =>
        {
            it.SelectedProcessId = this.GetValue(SelectedProcessIdProperty);
            it.SelectedThreadId = this.GetValue(SelectedThreadIdProperty);
        });
    }
    

    // Called when collection of visible logs changed.
    void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (this.GetValue(IsAllLogsSelectionRequestedProperty))
                    this.selectedLogs.AddAll(e.NewItems!.Cast<DisplayableLog>(), true);
                break;
            case NotifyCollectionChangedAction.Remove:
                this.selectedLogs.RemoveAll(e.OldItems!.Cast<DisplayableLog>(), true);
                break;
            case NotifyCollectionChangedAction.Reset:
                this.ClearSelectedLogs();
                break;
            default:
                throw new NotSupportedException($"Unsupported action of logs change: {e.Action}.");
        }
    }


    // Called when selected logs changed.
    void OnSelectedLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // ignore if disposed
        if (this.IsDisposed)
            return;
        
        // update state
        var selectionCount = this.selectedLogs.Count;
        if (selectionCount == 0)
            this.ResetValue(HasSelectedLogsProperty);
        else if (e.Action == NotifyCollectionChangedAction.Add && selectionCount == e.NewItems!.Count)
            this.SetValue(HasSelectedLogsProperty, true);

        // raise event and report time information
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            this.ResetValue(IsAllLogsSelectionRequestedProperty);
            this.notifySelectedLogsChangedAction.Execute();
            this.reportSelectedLogsTimeInfoAction.Reschedule();
        }
        else
        {
            this.notifySelectedLogsChangedAction.Schedule();
            this.reportSelectedLogsTimeInfoAction.Schedule(SelectedLogsTimeInfoReportingDelay);
        }

        // collect selected PID and TID
        var selectedPid = (int?)null;
        var selectedTid = (int?)null;
        if (selectionCount > 0 && selectionCount <= 64)
        {
            var samePid = true;
            var sameTid = true;
            for (var i = selectionCount - 1; i >= 0; --i)
            {
                var log = this.selectedLogs[i];
                if (samePid)
                {
                    var pid = log.ProcessId;
                    if (pid.HasValue)
                    {
                        if (!selectedPid.HasValue)
                            selectedPid = pid;
                        else if (pid != selectedPid)
                        {
                            selectedPid = null;
                            samePid = false;
                        }
                    }
                }
                if (sameTid)
                {
                    var tid = log.ThreadId;
                    if (tid.HasValue)
                    {
                        if (!selectedTid.HasValue)
                            selectedTid = tid;
                        else if (tid != selectedTid)
                        {
                            selectedTid = null;
                            sameTid = false;
                        }
                    }
                }
            }
        }
        this.SetValue(SelectedProcessIdProperty, selectedPid);
        this.SetValue(SelectedThreadIdProperty, selectedTid);
        this.DisplayableLogGroup?.Let(it =>
        {
            it.SelectedProcessId = selectedPid;
            it.SelectedThreadId = selectedTid;
        });

        // report selected value of property
        this.ReportSelectedLogPropertyValue();
    }


    // Report the value of selected log property.
    void ReportSelectedLogPropertyValue()
    {
        if (this.IsDisposed)
            return;
        var property = this.GetValue(SelectedLogPropertyProperty);
        this.Logger.LogInformation("Selection count: {count}, selected property: {property}", this.selectedLogs.Count, property?.Name);
        if (this.selectedLogs.Count != 1 || property == null)
        {
            this.ResetValue(SelectedLogPropertyValueProperty);
            this.ResetValue(SelectedLogStringPropertyValueProperty);
            return;
        }
        if (DisplayableLog.HasStringProperty(property.Name) && !property.Name.EndsWith("String"))
        {
            this.selectedLogs[0].TryGetProperty<string?>(property.Name, out var value);
            this.SetValue(SelectedLogPropertyValueProperty, value);
            this.SetValue(SelectedLogStringPropertyValueProperty, value);
        }
        else
        {
            this.selectedLogs[0].TryGetProperty<object?>(property.Name, out var value);
            this.SetValue(SelectedLogPropertyValueProperty, value);
            this.ResetValue(SelectedLogStringPropertyValueProperty);
        }
    }


    /// <summary>
    /// Select all logs.
    /// </summary>
    public void SelectAllLogs()
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        if (this.GetValue(IsAllLogsSelectionRequestedProperty))
            return;
        var logs = this.Session.Logs;
        this.selectedLogs.Clear();
        if (logs.IsNotEmpty())
        {
            this.SetValue(IsAllLogsSelectionRequestedProperty, true);
            this.selectedLogs.AddAll(logs, true);
        }
    }


    /// <summary>
    /// Command to select all logs.
    /// </summary>
    public ICommand SelectAllLogsCommand { get; }


    /// <summary>
    /// Get or set the log property which is currently selected.
    /// </summary>
    public DisplayableLogProperty? SelectedLogProperty
    {
        get => this.GetValue(SelectedLogPropertyProperty);
        set => this.SetValue(SelectedLogPropertyProperty, value);
    }


    /// <summary>
    /// Get value of selected log property.
    /// </summary>
    public object? SelectedLogPropertyValue => this.GetValue(SelectedLogPropertyValueProperty);


    /// <summary>
    /// Get list of selected logs.
    /// </summary>
    public IList<DisplayableLog> SelectedLogs { get => this.selectedLogs; }


    /// <summary>
    /// Raised when <see cref="SelectedLogs"/> changed.
    /// </summary>
    public event EventHandler? SelectedLogsChanged;


    /// <summary>
    /// Get duration between selected logs.
    /// </summary>
    public TimeSpan? SelectedLogsDuration { get => this.GetValue(SelectedLogsDurationProperty); }


    /// <summary>
    /// Get value of selected log property with string value.
    /// </summary>
    public string? SelectedLogStringPropertyValue => this.GetValue(SelectedLogStringPropertyValueProperty);


    // Select the log which represents the ending point of total duration of logs.
    void SelectLogDurationEndingLog()
    {
        // check state
        var session = this.Session;
        var profile = this.LogProfile;
        if (profile == null)
            return;
        var logs = session.Logs;
        if (logs.IsEmpty())
            return;
        if (!session.MaxLogTimeSpan.HasValue && !session.LatestLogTimestamp.HasValue)
            return;
        this.selectedLogs.Clear();
        if (profile.SortDirection == SortDirection.Ascending)
            this.selectedLogs.Add(logs[^1]);
        else
            this.selectedLogs.Add(logs[0]);
    }


    /// <summary>
    /// Command to select the log which represents the ending point of total duration of logs.
    /// </summary>
    public ICommand SelectLogDurationEndingLogCommand { get; }


    // Select the log which represents the starting point of total duration of logs.
    void SelectLogDurationStartingLog()
    {
        // check state
        var session = this.Session;
        var profile = this.LogProfile;
        if (profile == null)
            return;
        var logs = session.Logs;
        if (logs.IsEmpty())
            return;
        if (!session.MinLogTimeSpan.HasValue && !session.EarliestLogTimestamp.HasValue)
            return;
        this.selectedLogs.Clear();
        if (profile.SortDirection == SortDirection.Ascending)
            this.selectedLogs.Add(logs[0]);
        else
            this.selectedLogs.Add(logs[^1]);
    }


    /// <summary>
    /// Command to select the log which represents the starting point of total duration of logs.
    /// </summary>
    public ICommand SelectLogDurationStartingLogCommand { get; }


    /// <summary>
    /// Select all marked logs.
    /// </summary>
    public void SelectMarkedLogs()
    {
        // check state
        this.VerifyAccess();
        this.VerifyDisposed();

        // select logs
        this.selectedLogs.Clear();
        var logs = this.Session.Logs;
        var markedLogs = this.Session.MarkedLogs;
        for (var i = markedLogs.Count - 1; i >= 0; --i)
        {
            var log = markedLogs[i];
            if (logs.Contains(log))
                this.selectedLogs.Add(log);
        }
    }


    /// <summary>
    /// Command to select all marked logs.
    /// </summary>
    public ICommand SelectMarkedLogsCommand { get; }


    /// <summary>
    /// Select log which is nearest to given timestamp.
    /// </summary>
    /// <param name="timestamp">Timestamp.</param>
    /// <returns>Selected log.</returns>
    public DisplayableLog? SelectNearestLog(DateTime timestamp)
    {
        var log = this.Session.FindNearestLog(timestamp);
        if (log != null)
        {
            if (this.selectedLogs.Count != 1 || this.selectedLogs[0] != log)
            {
                this.selectedLogs.Clear();
                this.selectedLogs.Add(log);
            }
        }
        return log;
    }


    /// <summary>
    /// Get process ID of logs which has been selected by user.
    /// </summary>
    public int? SelectedProcessId { get => this.GetValue(SelectedProcessIdProperty); }


    /// <summary>
    /// Get thread ID of logs which has been selected by user.
    /// </summary>
    public int? SelectedThreadId { get => this.GetValue(SelectedThreadIdProperty); }
}