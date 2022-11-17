using CarinaStudio.Collections;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
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
    /// Property of <see cref="SelectedLogsDuration"/>.
    /// </summary>
    public static readonly ObservableProperty<TimeSpan?> SelectedLogsDurationProperty = ObservableProperty.Register<LogSelectionViewModel, TimeSpan?>(nameof(SelectedLogsDuration));
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
    readonly IDisposable logsObserverToken;
    readonly ScheduledAction notifySelectedLogsChangedAction;
    readonly ScheduledAction reportSelectedLogsTimeInfoAction;
    readonly SortedObservableList<DisplayableLog> selectedLogs;


    /// <summary>
    /// Initialize new <see cref="LogSelectionViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    public LogSelectionViewModel(Session session) : base(session)
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
        
        // attach to session
        this.logsObserverToken = session.GetValueAsObservable(Session.LogsProperty).Subscribe(logs =>
        {
            if (this.attachedLogs != null)
                this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
            this.ClearSelectedLogs();
            this.attachedLogs = logs as INotifyCollectionChanged;
            if (this.attachedLogs != null)
                this.attachedLogs.CollectionChanged += this.OnLogsChanged;
        });
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
        this.logsObserverToken.Dispose();

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