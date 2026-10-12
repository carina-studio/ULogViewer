using CarinaStudio.Collections;
using CarinaStudio.Diagnostics;
using CarinaStudio.Logging;
using CarinaStudio.Threading;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using CarinaStudio.ULogViewer.Text;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log selection.
/// </summary>
class LogSelectionViewModel : SessionComponent
{
    /// <summary>
    /// Property of <see cref="EarliestSelectedLogTimestamp"/>.
    /// </summary>
    public static readonly ObservableProperty<DateTime?> EarliestSelectedLogTimestampProp = ObservableProperty.Register<LogSelectionViewModel, DateTime?>(nameof(EarliestSelectedLogTimestamp));
    /// <summary>
    /// Property of <see cref="HasSelectedLogs"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> HasSelectedLogsProp = ObservableProperty.Register<LogSelectionViewModel, bool>(nameof(HasSelectedLogs));
    /// <summary>
    /// Property of <see cref="IsAllLogsSelectionRequested"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsAllLogsSelectionRequestedProp = ObservableProperty.Register<LogSelectionViewModel, bool>(nameof(IsAllLogsSelectionRequested));
    /// <summary>
    /// Property of <see cref="LatestSelectedLogTimestamp"/>.
    /// </summary>
    public static readonly ObservableProperty<DateTime?> LatestSelectedLogTimestampProp = ObservableProperty.Register<LogSelectionViewModel, DateTime?>(nameof(LatestSelectedLogTimestamp));
    /// <summary>
    /// Property of <see cref="SelectedLogProperty"/>.
    /// </summary>
    public static readonly ObservableProperty<DisplayableLogProperty?> SelectedLogPropertyProp = ObservableProperty.Register<LogSelectionViewModel, DisplayableLogProperty?>(nameof(SelectedLogProperty), coerce: (vm, p) =>
    {
        if (p is null)
            return null;
        return vm.Session.DisplayLogProperties.FirstOrDefault(it => it == p);
    });
    /// <summary>
    /// Property of <see cref="SelectedLogPropertyValue"/>.
    /// </summary>
    public static readonly ObservableProperty<object?> SelectedLogPropertyValueProp = ObservableProperty.Register<LogSelectionViewModel, object?>(nameof(SelectedLogPropertyValue));
    /// <summary>
    /// Property of <see cref="SelectedLogsDuration"/>.
    /// </summary>
    public static readonly ObservableProperty<TimeSpan?> SelectedLogsDurationProp = ObservableProperty.Register<LogSelectionViewModel, TimeSpan?>(nameof(SelectedLogsDuration));
    /// <summary>
    /// Property of <see cref="SelectedLogStringPropertyValue"/>.
    /// </summary>
    public static readonly ObservableProperty<IStringSource?> SelectedLogStringPropertyValueProp = ObservableProperty.Register<LogSelectionViewModel, IStringSource?>(nameof(SelectedLogStringPropertyValue));
    /// <summary>
    /// Property of <see cref="SelectedProcessId"/>.
    /// </summary>
    public static readonly ObservableProperty<int?> SelectedProcessIdProp = ObservableProperty.Register<LogSelectionViewModel, int?>(nameof(SelectedProcessId));
    /// <summary>
    /// Property of <see cref="SelectedThreadId"/>.
    /// </summary>
    public static readonly ObservableProperty<int?> SelectedThreadIdProp = ObservableProperty.Register<LogSelectionViewModel, int?>(nameof(SelectedThreadId));


    // Constant.
    const int SelectedLogsTimeInfoReportingDelay = 200;


    // Fields.
    INotifyCollectionChanged? attachedLogs;
    readonly MutableObservableBoolean canSelectLogsDurationEndingLog = new();
    readonly MutableObservableBoolean canSelectLogsDurationStartingLog = new();
    readonly ScheduledAction commitPendingSelectedLogsAction;
    readonly ScheduledAction notifySelectedLogsChangedAction;
    readonly HashSet<DisplayableLog> pendingSelectedLogs = new();
    readonly ScheduledAction reportSelectedLogsTimeInfoAction;
    readonly SortedObservableList<DisplayableLog> selectedLogs;
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    readonly ScheduledAction updateCanSelectLogsDurationStartingEndingLogsAction;


    /// <summary>
    /// Initialize new <see cref="LogSelectionViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogSelectionViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // create command
        var hasSelectedLogsObservable = this.GetValueAsObservable(HasSelectedLogsProp);
        this.ClearSelectedLogsCommand = new Command(this.ClearSelectedLogs, hasSelectedLogsObservable);
        this.CopySelectedLogsCommand = new Command(this.CopySelectedLogs, hasSelectedLogsObservable);
        this.CopySelectedLogsWithFileNamesCommand = new Command(this.CopySelectedLogsWithFileNames, new ForwardedObservableBoolean(ForwardedObservableBoolean.CombinationMode.And,
            false, 
            hasSelectedLogsObservable, 
            session.GetValueAsObservable(Session.IsLogFileSupportedProp)));
        this.SelectAllLogsCommand = new Command(this.SelectAllLogs, session.GetValueAsObservable(Session.HasLogsProp));
        this.SelectLogDurationEndingLogCommand = new Command(this.SelectLogDurationEndingLog, this.canSelectLogsDurationEndingLog);
        this.SelectLogDurationStartingLogCommand = new Command(this.SelectLogDurationStartingLog, this.canSelectLogsDurationStartingLog);
        this.SelectMarkedLogsCommand = new Command(this.SelectMarkedLogs, session.GetValueAsObservable(Session.HasMarkedLogsProp));

        // create collection
        this.selectedLogs = new SortedObservableList<DisplayableLog>(this.CompareLogs).Also(it =>
        {
            it.CollectionChanged += this.OnSelectedLogsChanged;
        });

        // create actions
        this.commitPendingSelectedLogsAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (this.pendingSelectedLogs.IsEmpty())
                return;
            var logs = this.pendingSelectedLogs.ToArray();
            this.pendingSelectedLogs.Clear();
            this.selectedLogs.AddAll(logs);
        });
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
            if (firstLog is null || lastLog is null || firstLog == lastLog)
            {
                this.ResetValue(SelectedLogsDurationProp);
                this.ResetValue(EarliestSelectedLogTimestampProp);
                this.ResetValue(LatestSelectedLogTimestampProp);
                return;
            }
            var duration = Session.CalculateDurationBetweenLogs(firstLog, lastLog, out _, out _, out var earliestTimestamp, out var latestTimestamp);
            if (duration.HasValue)
            {
                this.SetValue(SelectedLogsDurationProp, duration);
                this.SetValue(EarliestSelectedLogTimestampProp, earliestTimestamp);
                this.SetValue(LatestSelectedLogTimestampProp, latestTimestamp);
            }
            else
            {
                this.ResetValue(SelectedLogsDurationProp);
                this.ResetValue(EarliestSelectedLogTimestampProp);
                this.ResetValue(LatestSelectedLogTimestampProp);
            }
        });
        this.updateCanSelectLogsDurationStartingEndingLogsAction = new(() =>
        {
            this.canSelectLogsDurationEndingLog.Update(this.Session.LatestLogTimestamp.HasValue || this.Session.MaxLogTimeSpan.HasValue);
            this.canSelectLogsDurationStartingLog.Update(this.Session.EarliestLogTimestamp.HasValue || this.Session.MinLogTimeSpan.HasValue);
        });
        
        // attach to session
        this.AddResources(
            session.GetValueAsObservable(Session.DisplayLogPropertiesProp).Subscribe(properties =>
            {
                var selectedProperty = this.GetValue(SelectedLogPropertyProp);
                if (selectedProperty is not null && !properties.Contains(selectedProperty))
                    this.ResetValue(SelectedLogPropertyProp);
            }),
            session.GetValueAsObservable(Session.EarliestLogTimestampProp).Subscribe(_ =>
                this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule()),
            session.GetValueAsObservable(Session.LatestLogTimestampProp).Subscribe(_ =>
                this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule()),
            session.GetValueAsObservable(Session.LogsProp).Subscribe(logs =>
            {
                if (this.attachedLogs is not null)
                    this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
                this.ClearSelectedLogs();
                this.attachedLogs = logs as INotifyCollectionChanged;
                if (this.attachedLogs is not null)
                    this.attachedLogs.CollectionChanged += this.OnLogsChanged;
            }),
            session.GetValueAsObservable(Session.MaxLogTimeSpanProp).Subscribe(_ =>
                this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule()),
            session.GetValueAsObservable(Session.MinLogTimeSpanProp).Subscribe(_ =>
                this.updateCanSelectLogsDurationStartingEndingLogsAction.Schedule())
        );
        
        // attach to self
        this.GetValueAsObservable(IsAllLogsSelectionRequestedProp).Subscribe(requested =>
        {
            if (!requested)
            {
                this.pendingSelectedLogs.Clear();
                this.commitPendingSelectedLogsAction.Cancel();
            }
        });
        this.GetValueAsObservable(SelectedLogPropertyProp).Subscribe(this.ReportSelectedLogPropertyValue);
    }


    /// <summary>
    /// Clear all selected logs.
    /// </summary>
    public void ClearSelectedLogs()
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.SetValue(IsAllLogsSelectionRequestedProp, false);
        this.pendingSelectedLogs.Clear();
        this.commitPendingSelectedLogsAction.Cancel();
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
        if (this.attachedLogs is not null)
        {
            this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
            this.attachedLogs = null;
        }

        // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Get earliest timestamp of selected log.
    /// </summary>
	public DateTime? EarliestSelectedLogTimestamp => this.GetValue(EarliestSelectedLogTimestampProp);


    /// <summary>
    /// Check whether at least one log has been selected or not.
    /// </summary>
    public bool HasSelectedLogs => this.GetValue(HasSelectedLogsProp);


    /// <summary>
    /// Check whether selecting all logs has been requested or not.
    /// </summary>
    public bool IsAllLogsSelectionRequested => this.GetValue(IsAllLogsSelectionRequestedProp);


    /// <summary>
    /// Get latest timestamp of selected log.
    /// </summary>
	public DateTime? LatestSelectedLogTimestamp => this.GetValue(LatestSelectedLogTimestampProp);


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize
        + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.selectedLogs.Count);
    

    /// <inheritdoc/>
    protected override void OnDisplayableLogGroupCreated()
    {
        base.OnDisplayableLogGroupCreated();
        this.DisplayableLogGroup?.Let(it =>
        {
            it.SelectedProcessId = this.GetValue(SelectedProcessIdProp);
            it.SelectedThreadId = this.GetValue(SelectedThreadIdProp);
        });
    }
    

    // Called when collection of visible logs changed.
    void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (this.GetValue(IsAllLogsSelectionRequestedProp))
                {
                    this.pendingSelectedLogs.AddAll(e.NewItems!.Cast<DisplayableLog>());
                    this.commitPendingSelectedLogsAction.Schedule();
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLog>().Let(logs =>
                {
                    for (var i = logs.Count - 1; i >= 0; --i)
                        this.pendingSelectedLogs.Remove(logs[i]);
                    this.selectedLogs.RemoveAll(logs, true);
                });
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
            this.ResetValue(HasSelectedLogsProp);
        else if (e.Action == NotifyCollectionChangedAction.Add && selectionCount == e.NewItems!.Count)
            this.SetValue(HasSelectedLogsProp, true);

        // raise event and report time information
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            this.ResetValue(IsAllLogsSelectionRequestedProp);
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
        this.SetValue(SelectedProcessIdProp, selectedPid);
        this.SetValue(SelectedThreadIdProp, selectedTid);
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
        var property = this.GetValue(SelectedLogPropertyProp);
        this.Logger.LogInformation("Selection count: {count}, selected property: {property}", this.selectedLogs.Count, property?.Name);
        if (this.selectedLogs.Count != 1 || property is null)
        {
            this.ResetValue(SelectedLogPropertyValueProp);
            this.ResetValue(SelectedLogStringPropertyValueProp);
            return;
        }
        if (DisplayableLog.HasStringProperty(property.Name) && !property.Name.EndsWith("String"))
        {
            this.selectedLogs[0].TryGetProperty<IStringSource?>(property.Name, out var value);
            this.SetValue(SelectedLogPropertyValueProp, value);
            this.SetValue(SelectedLogStringPropertyValueProp, value);
        }
        else
        {
            this.selectedLogs[0].TryGetProperty<object?>(property.Name, out var value);
            this.SetValue(SelectedLogPropertyValueProp, value);
            this.ResetValue(SelectedLogStringPropertyValueProp);
        }
    }


    /// <summary>
    /// Select all logs.
    /// </summary>
    public void SelectAllLogs()
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        if (this.GetValue(IsAllLogsSelectionRequestedProp))
            return;
        var logs = this.Session.Logs;
        this.selectedLogs.Clear();
        if (logs.IsNotEmpty())
        {
            this.SetValue(IsAllLogsSelectionRequestedProp, true);
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
        get => this.GetValue(SelectedLogPropertyProp);
        set => this.SetValue(SelectedLogPropertyProp, value);
    }


    /// <summary>
    /// Get value of selected log property.
    /// </summary>
    public object? SelectedLogPropertyValue => this.GetValue(SelectedLogPropertyValueProp);


    /// <summary>
    /// Get list of selected logs.
    /// </summary>
    public IList<DisplayableLog> SelectedLogs => this.selectedLogs;


    /// <summary>
    /// Raised when <see cref="SelectedLogs"/> changed.
    /// </summary>
    public event EventHandler? SelectedLogsChanged;


    /// <summary>
    /// Get duration between selected logs.
    /// </summary>
    public TimeSpan? SelectedLogsDuration => this.GetValue(SelectedLogsDurationProp);


    /// <summary>
    /// Get value of selected log property with string value.
    /// </summary>
    public IStringSource? SelectedLogStringPropertyValue => this.GetValue(SelectedLogStringPropertyValueProp);


    // Select the log which represents the ending point of total duration of logs.
    void SelectLogDurationEndingLog()
    {
        // check state
        var session = this.Session;
        var profile = this.LogProfile;
        if (profile is null)
            return;
        var logs = session.Logs;
        if (logs.IsEmpty())
            return;
        if (!session.MaxLogTimeSpan.HasValue && !session.LatestLogTimestamp.HasValue)
            return;
        this.selectedLogs.Clear();
        this.selectedLogs.Add(profile.SortDirection == SortDirection.Ascending ? logs[^1] : logs[0]);
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
        if (profile is null)
            return;
        var logs = session.Logs;
        if (logs.IsEmpty())
            return;
        if (!session.MinLogTimeSpan.HasValue && !session.EarliestLogTimestamp.HasValue)
            return;
        this.selectedLogs.Clear();
        this.selectedLogs.Add(profile.SortDirection == SortDirection.Ascending ? logs[0] : logs[^1]);
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
        if (log is not null)
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
    public int? SelectedProcessId => this.GetValue(SelectedProcessIdProp);


    /// <summary>
    /// Get thread ID of logs which has been selected by user.
    /// </summary>
    public int? SelectedThreadId => this.GetValue(SelectedThreadIdProp);
}