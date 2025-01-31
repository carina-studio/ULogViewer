using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using CarinaStudio.ULogViewer.Logs.Profiles;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Base implementation of <see cref="IDisplayableLogProcessor"/>.
/// </summary>
abstract class BaseDisplayableLogProcessor<TProcessingToken, TProcessingResult> : BaseDisposableApplicationObject<IULogViewerApplication>, IDisplayableLogProcessor where TProcessingToken : class
{
    // Processing parameters.
    class ProcessingParams(TProcessingToken token)
    {
        public volatile int CompletedChunkId;
        public int ConcurrencyLevel;
        public int MaxConcurrencyLevel = 1;
        public int NextChunkId = 1;
        public readonly object ProcessingChunkLock = new();
        public readonly TProcessingToken Token = token;
    }


    // Fields.
    DisplayableLogGroup? attachedLogGroup; // currently only supports attaching to single group
    LogProfile? attachedLogProfile; // currently only supports attaching to single profile
    readonly long baseMemorySize;
    ProcessingParams? currentProcessingParams;
    volatile ILogger? logger;
    MemoryUsagePolicy memoryUsagePolicy;
    DisplayableLogProcessingPriority processingPriority;
    readonly ScheduledAction processNextChunkAction;
    IDisplayableLogComparer sourceLogComparer;
    IList<DisplayableLog> sourceLogs;
    readonly List<byte> sourceLogVersions; // Same order as source list
    readonly ScheduledAction startProcessingLogsAction;
    SortedObservableList<DisplayableLog> unprocessedLogs; // ASC: Reverse order of source list, DESC: Same order as source list


    /// <summary>
    /// Initialize new <see cref="BaseDisplayableLogProcessor{TProcessingToken, TProcessingResult}"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparer"><see cref="IDisplayableLogComparer"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected BaseDisplayableLogProcessor(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, IDisplayableLogComparer comparer, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app)
    {
        // get ID
        this.Id = BaseDisplayableLogProcessors.GetNextId();

        // create lists
        this.sourceLogVersions = new(sourceLogs.Count);
        this.unprocessedLogs = comparer.SortDirection == SortDirection.Ascending
            ? new(new Comparison<DisplayableLog>(comparer.Compare).Invert())
            : new(comparer.Compare);

        // setup properties
        this.baseMemorySize = Memory.EstimateInstanceSize(this.GetType(), 0);
        this.processingPriority = priority;
        this.ProcessingTaskFactory = BaseDisplayableLogProcessors.GetProcessingTaskFactory(priority);
        this.sourceLogComparer = comparer;
        this.sourceLogs = sourceLogs;

        // create schedule actions
        this.processNextChunkAction = new ScheduledAction(() =>
        {
            if (this.currentProcessingParams is not null)
                this.ProcessNextChunk(this.currentProcessingParams);
        });
        this.startProcessingLogsAction = new ScheduledAction(this.StartProcessingLogs);

        // attach to settings
        app.Settings.SettingChanged += this.OnSettingChanged;
        this.memoryUsagePolicy = app.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy);
        
        // attach to source logs
        (sourceLogs as INotifyCollectionChanged)?.Let(it => 
            it.CollectionChanged += this.OnSourceLogsChanged);
        
        // start processing
        this.InvalidateProcessing();
    }


    // Cancel current processing.
    void CancelProcessing(bool willStartProcessing)
    {
        // check state
        this.startProcessingLogsAction.Cancel();
        var processingParams = this.currentProcessingParams;
        if (processingParams is null)
            return;
        
        // log
        if (this.Application.IsDebugMode)
            this.Logger.LogTrace("Cancel current processing, unprocessed logs: {unprocessedLogsCount}", this.unprocessedLogs.Count);

        // cancel all chunk processing
        lock (processingParams.ProcessingChunkLock)
        {
            this.currentProcessingParams = null;
            Monitor.PulseAll(processingParams.ProcessingChunkLock);
        }
        this.processNextChunkAction.Cancel();

        // clear logs
        this.unprocessedLogs.Clear();
        if (!willStartProcessing && this.memoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
            this.unprocessedLogs.TrimExcess();

        // handle cancellation
        this.OnProcessingCancelled(processingParams.Token, willStartProcessing);

        // update state
        if (this.IsProcessing)
        {
            this.IsProcessing = false;
            this.Progress = 0;
            this.OnPropertyChanged(nameof(Progress));
            this.OnPropertyChanged(nameof(IsProcessing));
        }
    }


    /// <summary>
    /// Get size of processing chunk.
    /// </summary>
    protected virtual int ChunkSize => this.processingPriority switch
    {
        DisplayableLogProcessingPriority.Background => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingSizeBackground),
        DisplayableLogProcessingPriority.Realtime => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingSizeRealtime),
        _ => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingSizeDefault),
    };


    /// <summary>
    /// Compare nullable <see cref="DisplayableLog"/>s by <see cref="SourceLogComparer"/>.
    /// </summary>
    /// <param name="lhs">Left hand side log.</param>
    /// <param name="rhs">Right hand side log.</param>
    /// <returns>Comparison result.</returns>
    protected int CompareSourceLogs(DisplayableLog? lhs, DisplayableLog? rhs) =>
        this.sourceLogComparer.Compare(lhs, rhs);


    /// <summary>
    /// Create token for new processing.
    /// </summary>
    /// <param name="isProcessingNeeded">Whether processing is actually needed or not.</param>
    /// <returns>Token of processing.</returns>
    protected abstract TProcessingToken CreateProcessingToken(out bool isProcessingNeeded);


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // ignore if called from finalizer.
        if (!disposing)
            return;

        // check thread
        this.VerifyAccess();

        // detach from source logs
        (this.sourceLogs as INotifyCollectionChanged)?.Let(it => 
            it.CollectionChanged -= this.OnSourceLogsChanged);
        
        // detach from log profile
        if (this.attachedLogProfile is not null)
        {
            this.OnDetachFromLogProfile(this.attachedLogProfile);
            this.attachedLogProfile = null;
        }

        // detach from log group
        if (this.attachedLogGroup is not null)
        {
            this.OnDetachFromLogGroup(this.attachedLogGroup);
            this.attachedLogGroup = null;
        }

        // detach from settings
        this.Application.Settings.SettingChanged -= this.OnSettingChanged;

        // cancel processing
        this.CancelProcessing(false);
    }


    /// <summary>
    /// Raise when error message generated.
    /// </summary>
    public event Action<IDisplayableLogProcessor, MessageEventArgs>? ErrorMessageGenerated;


    /// <summary>
    /// Generate error message.
    /// </summary>
    /// <param name="message">Message.</param>
    protected void GenerateErrorMessage(string message)
    {
        if (this.CheckAccess())
        {
            if (!this.IsDisposed)
                this.ErrorMessageGenerated?.Invoke(this, new(message));
        }
        else
        {
            this.SynchronizationContext.Post(() => 
            {
                if (!this.IsDisposed)
                    this.ErrorMessageGenerated?.Invoke(this, new(message));
            });
        }
    }


    /// <summary>
    /// Get unique ID of the processor instance.
    /// </summary>
    protected int Id { get; }


    /// <summary>
    /// Notify that given log was updated and should be processed again.
    /// </summary>
    /// <param name="log">Log to be processed again.</param>
    public void InvalidateLog(DisplayableLog log)
    {
        // check state
#if DEBUG
        this.VerifyAccess();
#endif
        this.VerifyDisposed();
        if (this.currentProcessingParams is null)
            return;
        if (this.unprocessedLogs.Contains(log))
            return;

        // check source version
        var sourceIndex = this.sourceLogs.IndexOf(log);
        if (sourceIndex < 0)
            return;

        // update version to drop current result
        ++this.sourceLogVersions[sourceIndex];

        // handle invalidation
        if (this.OnLogInvalidated(log))
            return;

        // enqueue logs to unprocessed logs
        this.unprocessedLogs.Add(log);

        // start processing
        this.processNextChunkAction.Schedule();
    }


    /// <summary>
    /// Notify that given logs were updated and should be processed again.
    /// </summary>
    /// <param name="logs">Logs to be processed again.</param>
    public void InvalidateLogs(IEnumerable<DisplayableLog> logs)
    {
        // check state
#if DEBUG
        this.VerifyAccess();
#endif
        this.VerifyDisposed();
        if (this.currentProcessingParams is null)
            return;

        // enqueue logs to unprocessed logs
        var needToProcess = false;
        var sourceLogs = this.sourceLogs;
        var sourceLogVersions = this.sourceLogVersions;
        foreach (var log in logs)
        {
            if (!this.unprocessedLogs.Contains(log))
            {
                var sourceIndex = sourceLogs.IndexOf(log);
                if (sourceIndex >= 0)
                {
                    // handle invalidation
                    if (this.OnLogInvalidated(log))
                        continue;

                    // prepare for processing
                    ++sourceLogVersions[sourceIndex];
                    this.unprocessedLogs.Add(log);
                    needToProcess = true;
                }
            }
        }

        // start processing
        if (needToProcess)
        {
            this.processNextChunkAction.Cancel();
            for (var i = this.currentProcessingParams.MaxConcurrencyLevel; i > 0; --i)
                this.ProcessNextChunk(this.currentProcessingParams);
        }
    }
    
    
    /// <summary>
    /// Invalidate current processing and start new processing later.
    /// </summary>
    protected void InvalidateProcessing()
    {
#if DEBUG
        this.VerifyAccess();
#endif
        if (this.IsDisposed)
            return;
        var delay = this.processingPriority switch
        {
            DisplayableLogProcessingPriority.Default => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogProcessingDelayDefault),
            DisplayableLogProcessingPriority.Realtime => 0,
            _ => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogProcessingDelayBackground),
        };
        this.InvalidateProcessing(delay);
    }
    
    
    /// <summary>
    /// Invalidate current processing and start new processing later.
    /// </summary>
    /// <param name="delayMillis">Delay to restart processing in milliseconds.</param>
    protected void InvalidateProcessing(int delayMillis)
    {
#if DEBUG
        this.VerifyAccess();
#endif
        if (this.IsDisposed)
            return;
        if (this.Application.IsDebugMode && this.currentProcessingParams is not null)
        {
            this.Logger.LogTrace("Invalidate current processing");
            this.CancelProcessing(true);
        }
        this.startProcessingLogsAction.Reschedule(delayMillis);
    }


    /// <summary>
    /// Check whether the given token is the token of current processing or not.
    /// </summary>
    /// <param name="token">Token to check.</param>
    /// <returns>True if given token is the token of current processing.</returns>
    protected bool IsCurrentProcessingToken(TProcessingToken token) =>
        ReferenceEquals(this.currentProcessingParams?.Token, token);


    /// <summary>
    /// Check whether given log has been processed or not.
    /// </summary>
    /// <param name="log">Log to check.</param>
    /// <returns>True if the log has been processed.</returns>
    protected bool IsLogProcessed(DisplayableLog log) =>
        !this.unprocessedLogs.Contains(log);


    /// <summary>
    /// Check whether logs processing is on-going or not.
    /// </summary>
    public bool IsProcessing { get; private set; }


    /// <summary>
    /// Check whether logs processing is actually needed or not.
    /// </summary>
    public bool IsProcessingNeeded { get; private set; }


    /// <summary>
    /// Get logger.
    /// </summary>
    protected ILogger Logger =>
        this.logger ?? this.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}").Also(it =>
        {
            this.logger = it;
        });


    /// <summary>
    /// Get maximum concurrency level of processing.
    /// </summary>
    protected virtual int MaxConcurrencyLevel => 1;


    /// <summary>
    /// Get size of memory currently used by the instance directly in bytes.
    /// </summary>
    public virtual long MemorySize =>
        this.baseMemorySize 
        + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.unprocessedLogs.Count) 
        + Memory.EstimateCollectionInstanceSize(sizeof(byte), this.sourceLogVersions.Capacity);


    /// <inheritdoc/>
    public MemoryUsagePolicy MemoryUsagePolicy => this.memoryUsagePolicy;


    /// <summary>
    /// Called to attach to log group.
    /// </summary>
    /// <param name="group">Log group.</param>
    protected virtual void OnAttachToLogGroup(DisplayableLogGroup group)
    { }


    /// <summary>
    /// Called to attach to log profile.
    /// </summary>
    /// <param name="profile">Log profile.</param>
    protected virtual void OnAttachToLogProfile(LogProfile profile)
    {
        profile.PropertyChanged += this.OnLogProfilePropertyChanged;
    }


    // Called when chunk of logs processed.
    void OnChunkProcessed(ProcessingParams processingParams, int chunkId, List<DisplayableLog> logs, List<byte> logVersions, List<TProcessingResult> results)
    {
        // unlock next chunk
        lock (processingParams.ProcessingChunkLock)
        {
            if (processingParams.CompletedChunkId != chunkId - 1)
                throw new InternalStateCorruptedException("Incorrect order of completing chunk processing.");
            processingParams.CompletedChunkId = chunkId;
            Monitor.PulseAll(processingParams.ProcessingChunkLock);
        }

        // update state
        --processingParams.ConcurrencyLevel;

        // check state
        if (this.currentProcessingParams != processingParams)
            return;
        
        // remove logs which are not contained in source log list or out dated
        var sourceIndex = -1;
        var sourceLogs = this.sourceLogs;
        var sourceLogVersions = this.sourceLogVersions;
        var processedIndex = logs.Count - 1;
        var logComparison = new Comparison<DisplayableLog>(this.sourceLogComparer.Compare);
        while (processedIndex >= 0)
        {
            sourceIndex = sourceLogs.IndexOf(logs[processedIndex]);
            if (sourceIndex >= 0 && sourceLogVersions[sourceIndex] == logVersions[processedIndex])
            {
                --sourceIndex;
                --processedIndex;
                break;
            }
            logs.RemoveAt(processedIndex);
            results.RemoveAt(processedIndex--);
        }
        while (processedIndex >= 0 && sourceIndex >= 0)
        {
            var result = logComparison(sourceLogs[sourceIndex], logs[processedIndex]);
            if (result == 0)
            {
                if (sourceLogVersions[sourceIndex] == logVersions[processedIndex])
                    --processedIndex;
                else
                {
                    logs.RemoveAt(processedIndex);
                    results.RemoveAt(processedIndex--);
                }
                --sourceIndex;
            }
            else if (result > 0)
                --sourceIndex;
            else
            {
                logs.RemoveAt(processedIndex);
                results.RemoveAt(processedIndex--);
            }
        }
        while (processedIndex >= 0)
        {
            logs.RemoveAt(processedIndex);
            results.RemoveAt(processedIndex--);
        }

        // recycle list
        this.RecycleInternalDisplayableLogVersionList(logVersions);

        // handle processing result
        this.OnChunkProcessed(processingParams.Token, logs, results);

        // start processing next chunk
        this.ProcessNextChunk(processingParams);
    }


    /// <summary>
    /// Called when chunk of logs were processed.
    /// </summary>
    /// <param name="token">Token of processing.</param>
    /// <param name="logs">Processed logs.</param>
    /// <param name="results">Processing result.</param>
    protected abstract void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<TProcessingResult> results);


    /// <summary>
    /// Called to detach from log group.
    /// </summary>
    /// <param name="group">Log group.</param>
    protected virtual void OnDetachFromLogGroup(DisplayableLogGroup group)
    { }


    /// <summary>
    /// Called to detach from log profile.
    /// </summary>
    /// <param name="profile">Log profile.</param>
    protected virtual void OnDetachFromLogProfile(LogProfile profile)
    {
        profile.PropertyChanged -= this.OnLogProfilePropertyChanged;
    }


    /// <summary>
    /// Called when given log was invalidated.
    /// </summary>
    /// <param name="log">Log.</param>
    /// <returns>True if log can be handled directly for invalidation.</returns>
    protected abstract bool OnLogInvalidated(DisplayableLog log);


    // Called when property of attached log profile changed.
    void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        this.OnLogProfilePropertyChanged((LogProfile)sender.AsNonNull(), e);


    /// <summary>
    /// Called when property of attached log profile changed.
    /// </summary>
    /// <param name="profile">Log profile.</param>
    /// <param name="e">Event data.</param>
    protected virtual void OnLogProfilePropertyChanged(LogProfile profile, PropertyChangedEventArgs e)
    { }


    /// <summary>
    /// Called when <see cref="MemoryUsagePolicy"/> changed.
    /// </summary>
    protected virtual void OnMemoryUsagePolicyChanged()
    { }


    /// <summary>
    /// Called when current processing has been cancelled.
    /// </summary>
    /// <param name="token">Token of cancelled processing.</param>
    /// <param name="willStartProcessing">True if next processing will be started later.</param>
    protected abstract void OnProcessingCancelled(TProcessingToken token, bool willStartProcessing);


    /// <summary>
    /// Called to process log.
    /// </summary>
    /// <param name="token">Token of processing.</param>
    /// <param name="log">Log.</param>
    /// <param name="result">Result of processing.</param>
    /// <returns>True if processing result should be collected, False to drop the result.</returns>
    protected abstract bool OnProcessLog(TProcessingToken token, DisplayableLog log, out TProcessingResult result);

    
    /// <summary>
    /// Raise <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    protected virtual void OnPropertyChanged(string propertyName) => 
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    

    // Called when application setting changed.
    void OnSettingChanged(object? sender, SettingChangedEventArgs e) =>
        this.OnSettingChanged(e);
    

    /// <summary>
    /// Called when application setting changed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnSettingChanged(SettingChangedEventArgs e)
    {
        if (e.Key == SettingKeys.MemoryUsagePolicy)
        {
            this.memoryUsagePolicy = (MemoryUsagePolicy)e.Value;
            this.OnMemoryUsagePolicyChanged();
            this.OnPropertyChanged(nameof(MemoryUsagePolicy));
        }
    }
    

    // Called when source logs changed.
    void OnSourceLogsChanged(object? sender, NotifyCollectionChangedEventArgs e) => 
        this.OnSourceLogsChanged(e);
    

    /// <summary>
    /// Called when source logs changed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnSourceLogsChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.sourceLogVersions.InsertRange(e.NewStartingIndex, new byte[e.NewItems?.Count ?? 0]);
                if (this.currentProcessingParams is not null)
                {
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (this.sourceLogComparer.SortDirection == SortDirection.Ascending)
                        this.unprocessedLogs.AddAll(e.NewItems.AsNonNull().Cast<DisplayableLog>().Reverse(), true);
                    else
                        this.unprocessedLogs.AddAll(e.NewItems.AsNonNull().Cast<DisplayableLog>(), true);
                    this.processNextChunkAction.Cancel();
                    for (var i = this.currentProcessingParams.MaxConcurrencyLevel; i > 0; --i)
                        this.ProcessNextChunk(this.currentProcessingParams);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                {
                    var removedLogs = e.OldItems.AsNonNull().Cast<DisplayableLog>();
                    this.sourceLogVersions.RemoveRange(e.OldStartingIndex, e.OldItems?.Count ?? 0);
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (this.sourceLogComparer.SortDirection == SortDirection.Ascending)
                        this.unprocessedLogs.RemoveAll(removedLogs.Reverse(), true);
                    else
                        this.unprocessedLogs.RemoveAll(removedLogs, true);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
            {
                this.sourceLogVersions.Clear();
                if (this.sourceLogs.IsNotEmpty())
                    this.sourceLogVersions.AddRange(new byte[this.sourceLogs.Count]);
                else if (this.memoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
                    this.sourceLogVersions.TrimExcess();
                if (this.currentProcessingParams is not null)
                {
                    this.CancelProcessing(true);
                    this.startProcessingLogsAction.Reschedule();
                }
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported change of source log list: {e.Action}.");
        }
        if (this.sourceLogs.IsEmpty())
        {
            if (this.attachedLogProfile is not null)
            {
                this.OnDetachFromLogProfile(this.attachedLogProfile);
                this.attachedLogProfile = null;
            }
            if (this.attachedLogGroup is not null)
            {
                this.OnDetachFromLogGroup(this.attachedLogGroup);
                this.attachedLogGroup = null;
            }
        }
    }
    

    // Process chunk of logs.
    async Task ProcessChunk(ProcessingParams processingParams, int chunkId, List<DisplayableLog> logs, List<byte> logVersions, SortDirection sortDirection)
    {
        // check state
        if (this.currentProcessingParams != processingParams)
            return;

        // process logs
        var logCount = logs.Count;
        var processedLogs = new List<DisplayableLog>();
        var processingResults = new List<TProcessingResult>(logCount);
        var token = processingParams.Token;
        for (var i = logCount - 1; i >= 0 && this.currentProcessingParams == processingParams; --i) // Need to process in reverse order to make sure the processing order is same as source list
        {
            var log = logs[i];
            if (this.OnProcessLog(token, log, out var result))
            {
                processedLogs.Add(log);
                processingResults.Add(result);
            }
            else
                logVersions.RemoveAt(i);
        }
        
        // Reverse back to same order as source list
        if (sortDirection == SortDirection.Ascending)
            logVersions.Reverse(); 
        else
            processedLogs.Reverse();

        // recycle list
        this.RecycleInternalDisplayableLogList(logs);

        // wait for previous chunks
        var paddingInterval = this.processingPriority switch
        {
            DisplayableLogProcessingPriority.Default => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingPaddingIntervalDefault),
            DisplayableLogProcessingPriority.Realtime => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingPaddingIntervalRealtime),
            _ => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingPaddingIntervalBackground),
        };
        if (paddingInterval > 0)
            await Task.Delay(paddingInterval);
        while (true)
        {
            lock (processingParams.ProcessingChunkLock)
            {
                if (this.currentProcessingParams != processingParams)
                    return;
                if (processingParams.CompletedChunkId == chunkId - 1)
                {
                    this.SynchronizationContext.Post(() => this.OnChunkProcessed(processingParams, chunkId, processedLogs, logVersions, processingResults));
                    return;
                }
                Monitor.Wait(processingParams.ProcessingChunkLock);
            }
        }
    }


    /// <summary>
    /// Get or set priority of logs processing.
    /// </summary>
    public DisplayableLogProcessingPriority ProcessingPriority
    {
        get => this.processingPriority;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.processingPriority == value)
                return;
            this.processingPriority = value;
            this.currentProcessingParams?.Let(it =>
            {
                it.MaxConcurrencyLevel = Math.Min(BaseDisplayableLogProcessors.GetMaxConcurrencyLevel(this.processingPriority), Math.Max(1, this.MaxConcurrencyLevel));
            });
            this.OnPropertyChanged(nameof(ProcessingPriority));
        }
    }
    

    /// <summary>
    /// Get <see cref="TaskFactory"/> of processing tasks.
    /// </summary>
    protected TaskFactory ProcessingTaskFactory { get; }


    // Start processing next chunk.
    void ProcessNextChunk(ProcessingParams processingParams)
    {
        // check state
        if (this.currentProcessingParams != processingParams)
            return;
        if (this.unprocessedLogs.IsEmpty())
        {
            if (this.IsProcessing)
            {
                this.IsProcessing = false;
                this.Progress = 1;
                this.OnPropertyChanged(nameof(Progress));
                this.OnPropertyChanged(nameof(IsProcessing));
            }
            return;
        }
        if (processingParams.ConcurrencyLevel >= processingParams.MaxConcurrencyLevel)
            return;

        // update state
        if (!this.IsProcessing)
        {
            this.IsProcessing = true;
            this.OnPropertyChanged(nameof(IsProcessing));
        }
        this.Progress = 1.0 - ((double)this.unprocessedLogs.Count / this.sourceLogs.Count);
        this.OnPropertyChanged(nameof(Progress));

        // attach to log profile
        var group = this.unprocessedLogs[0].GroupOrNull;
        var profile = group?.LogProfile;
        if (group != this.attachedLogGroup)
        {
            if (this.attachedLogGroup is not null)
                this.OnDetachFromLogGroup(this.attachedLogGroup);
            this.attachedLogGroup = group;
            if (group is not null)
                this.OnAttachToLogGroup(group);
        }
        if (!ReferenceEquals(profile, this.attachedLogProfile))
        {
            if (this.attachedLogProfile is not null)
                this.OnDetachFromLogProfile(this.attachedLogProfile);
            this.attachedLogProfile = profile;
            if (profile is not null)
                this.OnAttachToLogProfile(profile);
        }

        // start processing
        var chunkSize = Math.Max(64, this.ChunkSize);
        var chunkId = processingParams.NextChunkId++;
        var logs = this.unprocessedLogs.Let(it =>
        {
            if (it.Count <= chunkSize)
            {
                var list = this.ObtainInternalDisplayableLogList(it);
                it.Clear();
                if (this.memoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
                    it.TrimExcess();
                return list;
            }
            else
            {
                var index = it.Count - chunkSize;
                var list = this.ObtainInternalDisplayableLogList(it.GetRangeView(index, chunkSize));
                it.RemoveRange(index, chunkSize);
                return list;
            }
        });
        var sortDirection = this.sourceLogComparer.SortDirection;
        var logVersions = this.ObtainInternalDisplayableLogVersionList().Also(it => // The order will be reverse order of source list
        {
            var sourceLogs = this.sourceLogs;
            var sourceLogVersions = this.sourceLogVersions;
            var comparer = this.unprocessedLogs.Comparer;
            var logCount = logs.Count;
            var unprocessedIndex = 0;
            var sourceIndex = sourceLogs.IndexOf(logs[0]);
            while (sourceIndex < 0 && unprocessedIndex < logCount - 1)
                sourceIndex = sourceLogs.IndexOf(logs[++unprocessedIndex]);
            if (sortDirection == SortDirection.Ascending)
            {
                if (unprocessedIndex < logCount && sourceIndex >= 0)
                {
                    it.EnsureCapacity(logCount);
                    while (true)
                    {
                        var comparisonResult = comparer.Compare(logs[unprocessedIndex], sourceLogs[sourceIndex]);
                        if (comparisonResult == 0)
                        {
                            ++unprocessedIndex;
                            it.Add(sourceLogVersions[sourceIndex--]);
                            if (unprocessedIndex >= logCount || sourceIndex < 0)
                                break;
                        }
                        else if (comparisonResult < 0)
                        {
                            ++unprocessedIndex;
                            if (unprocessedIndex >= logCount)
                                break;
                        }
                        else
                        {
                            --sourceIndex;
                            if (sourceIndex < 0)
                                break;
                        }
                    }
                }
            }
            else
            {
                var sourceLogCount = sourceLogs.Count;
                if (unprocessedIndex < logCount && sourceIndex < sourceLogCount)
                {
                    it.EnsureCapacity(logCount);
                    while (true)
                    {
                        var comparisonResult = comparer.Compare(logs[unprocessedIndex], sourceLogs[sourceIndex]);
                        if (comparisonResult == 0)
                        {
                            ++unprocessedIndex;
                            it.Add(sourceLogVersions[sourceIndex++]);
                            if (unprocessedIndex >= logCount || sourceIndex >= sourceLogCount)
                                break;
                        }
                        else if (comparisonResult < 0)
                        {
                            ++unprocessedIndex;
                            if (unprocessedIndex >= logCount)
                                break;
                        }
                        else
                        {
                            ++sourceIndex;
                            if (sourceIndex >= sourceLogCount)
                                break;
                        }
                    }
                }
            }
        });
        ++processingParams.ConcurrencyLevel;
        this.ProcessingTaskFactory.StartNew(() => this.ProcessChunk(processingParams, chunkId, logs, logVersions, sortDirection));
    }


    /// <summary>
    /// Get current progress of processing.
    /// </summary>
    public double Progress { get; private set; }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <inheritdoc/>
    public IDisplayableLogComparer SourceLogComparer
    {
        get => this.sourceLogComparer;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.sourceLogComparer.Equals(value))
                return;
            this.sourceLogComparer = value;
            this.sourceLogVersions.Clear();
            this.sourceLogVersions.AddRange(new byte[this.sourceLogs.Count]);
            this.CancelProcessing(true);
            this.unprocessedLogs = value.SortDirection == SortDirection.Ascending
                ? new(new Comparison<DisplayableLog>(value.Compare).Invert())
                : new(value.Compare);
            this.startProcessingLogsAction.Reschedule();
        }
    }


    /// <inheritdoc/>
    public IList<DisplayableLog> SourceLogs
    { 
        get => this.sourceLogs;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (ReferenceEquals(this.sourceLogs, value))
                return;
            (this.sourceLogs as INotifyCollectionChanged)?.Let(it =>
                it.CollectionChanged -= this.OnSourceLogsChanged);
            (value as INotifyCollectionChanged)?.Let(it =>
                it.CollectionChanged += this.OnSourceLogsChanged);
            this.sourceLogs = value;
            this.sourceLogVersions.Clear();
            this.sourceLogVersions.AddRange(new byte[value.Count]);
            this.CancelProcessing(true);
            this.startProcessingLogsAction.Reschedule();
        }
    }


    // Start processing logs.
    void StartProcessingLogs()
    {
        // check state
        if (this.IsDisposed)
            return;

        // cancel current processing
        this.CancelProcessing(true);

        // create token
        var processingToken = this.CreateProcessingToken(out var isProcessingNeeded);

        // no need to process
        if (!isProcessingNeeded)
        {
            if (this.Application.IsDebugMode)
                this.Logger.LogTrace("No need to processing logs");
            this.Progress = 0;
            this.OnPropertyChanged(nameof(Progress));
            if (this.IsProcessing)
            {
                this.IsProcessing = false;
                this.OnPropertyChanged(nameof(IsProcessing));
            }
            if (this.IsProcessingNeeded)
            {
                this.IsProcessingNeeded = false;
                this.OnPropertyChanged(nameof(IsProcessingNeeded));
            }
            return;
        }
        if (this.Application.IsDebugMode)
            this.Logger.LogTrace("Start processing {sourceLogCount} logs", this.sourceLogs.Count);

        // start processing
        if (!this.IsProcessingNeeded)
        {
            this.IsProcessingNeeded = true;
            this.OnPropertyChanged(nameof(IsProcessingNeeded));
        }
        this.currentProcessingParams = new ProcessingParams(processingToken).Also(it => 
        {
            it.MaxConcurrencyLevel = Math.Min(BaseDisplayableLogProcessors.GetMaxConcurrencyLevel(this.processingPriority), Math.Max(1, this.MaxConcurrencyLevel));
        });
        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (this.sourceLogComparer.SortDirection == SortDirection.Ascending)
            this.unprocessedLogs.AddAll(this.sourceLogs.Reverse(), true);
        else
            this.unprocessedLogs.AddAll(this.sourceLogs, true);
        for (var i = this.currentProcessingParams.MaxConcurrencyLevel; i > 0; --i)
            this.ProcessNextChunk(this.currentProcessingParams);
    }


    /// <inheritdoc/>
    public override string ToString() =>
        $"{this.GetType().Name}-{this.Id}";
}


/// <summary>
/// Shared resources of <see cref="BaseDisplayableLogProcessor{TProcessingToken, TProcessingResult}"/>.
/// </summary>
static class BaseDisplayableLogProcessors
{
    /// <summary>
    /// Max concurrency level of processing with background priority.
    /// </summary>
    public static readonly int MaxProcessingConcurrencyLevelBackground = 1;
    /// <summary>
    /// Max concurrency level of processing with default priority.
    /// </summary>
    public static readonly int MaxProcessingConcurrencyLevelDefault = 2;
    /// <summary>
    /// Max concurrency level of processing with realtime priority.
    /// </summary>
    public static readonly int MaxProcessingConcurrencyLevelRealtime = Math.Min(4, Math.Max(1, Environment.ProcessorCount >> 1));


    // Constants.
    const int ListPoolCapacity = 32;


    // Static fields.
    static readonly Stack<List<DisplayableLog>> InternalDisplayableLogListPool = new();
    static readonly Stack<List<byte>> InternalDisplayableLogVersionListPool = new();
    static int NextId;
    static volatile TaskFactory? ProcessingTaskFactoryBackground;
    static volatile TaskFactory? ProcessingTaskFactoryDefault;
    static volatile TaskFactory? ProcessingTaskFactoryRealtime;
    static readonly object ProcessingTaskFactorySyncLock = new();


    /// <summary>
    /// Get maximum supported concurrency level of logs processing.
    /// </summary>
    /// <param name="priority">Priority of processing.</param>
    /// <returns>Maximum concurrency level.</returns>
    public static int GetMaxConcurrencyLevel(DisplayableLogProcessingPriority priority) => priority switch
    {
        DisplayableLogProcessingPriority.Default => MaxProcessingConcurrencyLevelDefault,
        DisplayableLogProcessingPriority.Realtime => MaxProcessingConcurrencyLevelRealtime,
        _ => MaxProcessingConcurrencyLevelBackground,
    };


    /// <summary>
    /// Get next unique ID for processor instance.
    /// </summary>
    /// <returns>ID.</returns>
    public static int GetNextId() => 
        Interlocked.Increment(ref NextId);
    

    /// <summary>
    /// Get <see cref="TaskFactory"/> for processing logs.
    /// </summary>
    /// <param name="priority">Priority.</param>
    /// <returns><see cref="TaskFactory"/>.</returns>
    public static TaskFactory GetProcessingTaskFactory(DisplayableLogProcessingPriority priority)
    {
        var taskFactory = priority switch
        {
            DisplayableLogProcessingPriority.Default => ProcessingTaskFactoryDefault,
            DisplayableLogProcessingPriority.Realtime => ProcessingTaskFactoryRealtime,
            _ => ProcessingTaskFactoryBackground,
        };
        if (taskFactory is not null)
            return taskFactory;
        // ReSharper disable NonAtomicCompoundOperator
        return ProcessingTaskFactorySyncLock.Lock(_ => 
        {
            switch (priority)
            {
                case DisplayableLogProcessingPriority.Default:
                    ProcessingTaskFactoryDefault ??= new(new FixedThreadsTaskScheduler(MaxProcessingConcurrencyLevelDefault << 1));
                    return ProcessingTaskFactoryDefault;
                case DisplayableLogProcessingPriority.Realtime:
                    ProcessingTaskFactoryRealtime ??= new(new FixedThreadsTaskScheduler(MaxProcessingConcurrencyLevelRealtime << 1));
                    return ProcessingTaskFactoryRealtime;
                default:
                    ProcessingTaskFactoryBackground ??= new(new FixedThreadsTaskScheduler(MaxProcessingConcurrencyLevelBackground << 1));
                    return ProcessingTaskFactoryBackground;
            }
        });
        // ReSharper restore NonAtomicCompoundOperator
    }
    

    /// <summary>
    /// Obtain an list of log for internal usage.
    /// </summary>
    /// <param name="processor">Log processor.</param>
    /// <param name="elements">Initial elements.</param>
    /// <returns>List.</returns>
#pragma warning disable IDE0060
    public static List<DisplayableLog> ObtainInternalDisplayableLogList(this IDisplayableLogProcessor processor, IEnumerable<DisplayableLog>? elements = null)
#pragma warning restore IDE0060
    {
        var list = InternalDisplayableLogListPool.Lock(it =>
        {
            it.TryPop(out var list);
            return list;
        });
        if (list is not null)
        {
            if (elements is not null)
                list.AddRange(elements);
            return list;
        }
        if (elements is not null)
            return [..elements];
        return [];
    }


    /// <summary>
    /// Obtain an list of version of log for internal usage.
    /// </summary>
    /// <param name="processor">Log processor.</param>
    /// <param name="elements">Initial elements.</param>
    /// <returns>List.</returns>
#pragma warning disable IDE0060
    public static List<byte> ObtainInternalDisplayableLogVersionList(this IDisplayableLogProcessor processor, IEnumerable<byte>? elements = null)
    {
#pragma warning restore IDE0060
        var list = InternalDisplayableLogVersionListPool.Lock(it =>
        {
            it.TryPop(out var list);
            return list;
        });
        if (list is not null)
        {
            if (elements is not null)
                list.AddRange(elements);
            return list;
        }
        if (elements is not null)
            return [..elements];
        return [];
    }
    

    /// <summary>
    /// Recycle the list of log for internal usage.
    /// </summary>
    /// <param name="processor">Log processor.</param>
    /// <param name="list">List to recycle.</param>
    public static void RecycleInternalDisplayableLogList(this IDisplayableLogProcessor processor, List<DisplayableLog> list)
    {
        list.Clear();
        lock (InternalDisplayableLogListPool)
        {
            if (processor.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
                InternalDisplayableLogListPool.Clear();
            else if (InternalDisplayableLogListPool.Count < ListPoolCapacity)
                InternalDisplayableLogListPool.Push(list);
        }
    }


    /// <summary>
    /// Recycle the list of version of log for internal usage.
    /// </summary>
    /// <param name="processor">Log processor.</param>
    /// <param name="list">List to recycle.</param>
    public static void RecycleInternalDisplayableLogVersionList(this IDisplayableLogProcessor processor, List<byte> list)
    {
        list.Clear();
        lock (InternalDisplayableLogVersionListPool)
        {
            if (processor.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
                InternalDisplayableLogVersionListPool.Clear();
            else if (InternalDisplayableLogVersionListPool.Count < ListPoolCapacity)
                InternalDisplayableLogVersionListPool.Push(list);
        }
    }
}