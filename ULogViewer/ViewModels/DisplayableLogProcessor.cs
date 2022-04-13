using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Base class to process list of <see cref="DisplayableLog"/> in background.
/// </summary>
abstract class DisplayableLogProcessor<TProcessingToken, TProcessingResult> : BaseDisposableApplicationObject<IULogViewerApplication>, INotifyPropertyChanged where TProcessingToken : class
{
    // Processing parameters.
    class ProcessingParams
    {
        // Fields.
        public volatile int CompletedChunkId;
        public int ConcurrencyLevel;
        public int NextChunkId = 1;
        public readonly object ProcessingChunkLock = new object();
        public readonly TProcessingToken Token;

        // Constructor.
        public ProcessingParams(TProcessingToken token) =>
            this.Token = token;
    }
    // Fields.
    ProcessingParams? currentProcessingParams;
    readonly Comparison<DisplayableLog> logComparison;
    volatile TaskFactory? processingTaskFactory;
    volatile FixedThreadsTaskScheduler? processingTaskScheduler;
    readonly ScheduledAction processNextChunkAction;
    readonly List<byte> sourceLogVersions;
    readonly ScheduledAction startProcessingLogsAction;
    readonly SortedObservableList<DisplayableLog> unprocessedLogs;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogProcessor"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    protected DisplayableLogProcessor(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app)
    {
        // create lists
        this.sourceLogVersions = sourceLogs.Count.Let(it =>
        {
            return new List<byte>(new byte[it]);
        });
        this.unprocessedLogs = new SortedObservableList<DisplayableLog>(comparison.Invert());

        // setup properties
        this.logComparison = comparison;
        this.SourceLogs = sourceLogs;

        // create schedule actions
        this.processNextChunkAction = new ScheduledAction(() =>
        {
            if (this.currentProcessingParams != null)
                this.ProcessNextChunk(this.currentProcessingParams);
        });
        this.startProcessingLogsAction = new ScheduledAction(this.StartProcessingLogs);
        
        // attach to source logs
        (sourceLogs as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += this.OnSourceLogsChanged);
    }


    // Cancel current processing.
    void CancelProcessing()
    {
        // check state
        var processingParams = this.currentProcessingParams;
        if (processingParams == null)
            return;

        // cancel all chunk processing
        lock (processingParams.ProcessingChunkLock)
        {
            this.currentProcessingParams = null;
            Monitor.PulseAll(processingParams.ProcessingChunkLock);
        }
        this.processNextChunkAction.Cancel();

        // clear logs
        this.unprocessedLogs.Clear();

        // handle cancellation
        this.OnProcessingCancelled(processingParams.Token);

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
    /// Get size of ptocessing chunk.
    /// </summary>
    protected virtual int ChunkSize { get => this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingSize); }


    /// <summary>
    /// Create token for new processing.
    /// </summary>
    /// <param name="isProcessingNeeded">Whether processing is actually needed or not.</param>
    /// <returns>Token of processing.</returns>
    protected abstract TProcessingToken CreateProcessingToken(out bool isProcessingNeeded);


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // dispose task scheduler only
        if (!disposing)
        {
            lock (this)
            {
                (this.processingTaskScheduler as IDisposable)?.Dispose();
                this.processingTaskScheduler = null;
                this.processingTaskFactory = null;
            }
        }

        // check thread
        this.VerifyAccess();

        // detach from source logs
        (this.SourceLogs as INotifyCollectionChanged)?.Let(it => it.CollectionChanged -= this.OnSourceLogsChanged);

        // cancecl processing
        this.CancelProcessing();

        // dispose task scheduler
        lock (this)
        {
            (this.processingTaskScheduler as IDisposable)?.Dispose();
            this.processingTaskScheduler = null;
            this.processingTaskFactory = null;
        }
    }


    /// <summary>
    /// Notify that given log was updated and should be processed again.
    /// </summary>
    /// <param name="log">Log to be processed again.</param>
    public void InvalidateLog(DisplayableLog log)
    {
        // check state
        this.VerifyAccess();
        this.VerifyDisposed();
        if (this.currentProcessingParams == null)
            return;
        if (this.unprocessedLogs.Contains(log))
            return;

        // check source version
        var sourceIndex = this.SourceLogs.IndexOf(log);
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
        this.VerifyAccess();
        this.VerifyDisposed();
        if (this.currentProcessingParams == null)
            return;

        // enqueue logs to unprocessed logs
        var needToProcess = false;
        var sourceLogs = this.SourceLogs;
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
            for (var i = this.MaxConcurrencyLevel; i > 0; --i)
                this.ProcessNextChunk(this.currentProcessingParams);
        }
    }


    /// <summary>
    /// Invalidate current processing and start new processing later.
    /// </summary>
    protected void InvalidateProcessing()
    {
        this.VerifyAccess();
        if (this.IsDisposed)
            return;
        this.startProcessingLogsAction.Schedule();
    }


    /// <summary>
    /// Check whether logs processing is on-going or not.
    /// </summary>
    public bool IsProcessing { get; private set; }


    /// <summary>
    /// Check whether logs processing is actually needed or not.
    /// </summary>
    public bool IsProcessingNeeded { get; private set; }


    /// <summary>
    /// Get maximum concurrency level of processing.
    /// </summary>
    protected virtual int MaxConcurrencyLevel { get => 1; }


    // Called when chunk of logs processed.
    void OnChunkProcessed(ProcessingParams processingParams, int chunkId, IList<DisplayableLog> logs, IList<byte> logVersions, IList<TProcessingResult> results)
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
        var sourceLogs = this.SourceLogs;
        var sourceLogVersions = this.sourceLogVersions;
        var processedIndex = logs.Count - 1;
        var logComparison = this.logComparison;
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
    protected abstract void OnChunkProcessed(TProcessingToken token, IList<DisplayableLog> logs, IList<TProcessingResult> results);


    /// <summary>
    /// Called when given log was invalidated.
    /// </summary>
    /// <param name="log">Log.</param>
    /// <returns>True if log can be handled directly for invalidation.</returns>
    protected abstract bool OnLogInvalidated(DisplayableLog log);


    /// <summary>
    /// Called when current processing has been cancelled.
    /// </summary>
    /// <param name="token">Token of cancelled processing.</param>
    protected abstract void OnProcessingCancelled(TProcessingToken token);


    /// <summary>
    /// Called to process log.
    /// </summary>
    /// <param name="token">Token of processing.</param>
    /// <param name="log">Log.</param>
    /// <returns>Processing result.</returns>
    protected abstract TProcessingResult OnProcessLog(TProcessingToken token, DisplayableLog log);

    
    /// <summary>
    /// Raise <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    protected virtual void OnPropertyChanged(string propertyName) => 
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    

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
                if (this.currentProcessingParams != null)
                {
                    this.unprocessedLogs.AddAll(e.NewItems.AsNonNull().Cast<DisplayableLog>().Reverse(), true);
                    this.processNextChunkAction.Cancel();
                    for (var i = this.MaxConcurrencyLevel; i > 0; --i)
                        this.ProcessNextChunk(this.currentProcessingParams);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                {
                    var removedLogs = e.OldItems.AsNonNull().Cast<DisplayableLog>();
                    this.sourceLogVersions.RemoveRange(e.OldStartingIndex, e.OldItems?.Count ?? 0);
                    this.unprocessedLogs.RemoveAll(removedLogs.Reverse(), true);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                this.sourceLogVersions.Clear();
                this.sourceLogVersions.AddRange(new byte[this.SourceLogs.Count]);
                this.InvalidateProcessing();
                break;
            default:
                throw new InvalidOperationException($"Unsupported change of source log list: {e.Action}.");
        }
    }
    

    // Process chunk of logs.
    void ProcessChunk(ProcessingParams processingParams, int chunkId, IList<DisplayableLog> logs, IList<byte> logVersions)
    {
        // check state
        if (this.currentProcessingParams != processingParams)
            return;

        // process logs
        var logCount = logs.Count;
        var processingResults = new List<TProcessingResult>(logCount);
        for (var i = 0; i < logCount; ++i)
            processingResults.Add(this.OnProcessLog(processingParams.Token, logs[i]));

        // wait for previous chunks
        var paddingInterval = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DisplayableLogChunkProcessingPaddingInterval);
        if (paddingInterval > 0)
            Thread.Sleep(paddingInterval);
        while (true)
        {
            lock (processingParams.ProcessingChunkLock)
            {
                if (this.currentProcessingParams != processingParams)
                    return;
                if (processingParams.CompletedChunkId == chunkId - 1)
                {
                    this.SynchronizationContext.Post(() => this.OnChunkProcessed(processingParams, chunkId, logs, logVersions, processingResults));
                    return;
                }
                Monitor.Wait(processingParams.ProcessingChunkLock);
            }
        }
    }
    

    /// <summary>
    /// Get <see cref="TaskFactory"/> of processing tasks.
    /// </summary>
    protected TaskFactory ProcessingTaskFactory
    { 
        get
        {
            if (this.processingTaskFactory != null)
                return this.processingTaskFactory;
            lock (this)
            {
                if (this.processingTaskFactory == null)
                {
                    this.VerifyDisposed();
                    this.processingTaskScheduler = new FixedThreadsTaskScheduler(Math.Max(1, this.MaxConcurrencyLevel));
                    this.processingTaskFactory = new TaskFactory(this.processingTaskScheduler);
                }
                return this.processingTaskFactory;
            }
        }   
    }


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
        if (processingParams.ConcurrencyLevel >= this.MaxConcurrencyLevel)
            return;

        // update state
        if (!this.IsProcessing)
        {
            this.IsProcessing = true;
            this.OnPropertyChanged(nameof(IsProcessing));
        }
        this.Progress = 1.0 - ((double)this.unprocessedLogs.Count / this.SourceLogs.Count);
        this.OnPropertyChanged(nameof(Progress));

        // start processing
        var chunkSize = Math.Max(64, this.ChunkSize);
        var chunkId = processingParams.NextChunkId++;
        var logs = this.unprocessedLogs.Let(it =>
        {
            if (it.Count <= chunkSize)
            {
                var array = it.ToArray();
                it.Clear();
                return array;
            }
            else
            {
                var index = it.Count - chunkSize;
                var array = it.ToArray(index, chunkSize);
                it.RemoveRange(index, chunkSize);
                return array;
            }
        });
        var logVersions = new byte[logs.Length].Also(it =>
        {
            var sourceLogs = this.SourceLogs;
            var sourceLogVersions = this.sourceLogVersions;
            var comparer = this.unprocessedLogs.Comparer;
            var logCount = it.Length;
            var unprocessedIndex = 0;
            var sourceIndex = sourceLogs.IndexOf(logs[0]);
            while (sourceIndex < 0 && unprocessedIndex < logCount - 1)
                sourceIndex = sourceLogs.IndexOf(logs[++unprocessedIndex]);
            if (unprocessedIndex < logCount && sourceIndex >= 0)
            {
                while (true)
                {
                    var comparisonResult = comparer.Compare(logs[unprocessedIndex], sourceLogs[sourceIndex]);
                    if (comparisonResult == 0)
                    {
                        it[unprocessedIndex++] = sourceLogVersions[sourceIndex--];
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
        });
        ++processingParams.ConcurrencyLevel;
        this.ProcessingTaskFactory.StartNew(() => this.ProcessChunk(processingParams, chunkId, logs, logVersions));
    }


    /// <summary>
    /// Get current progress of processing.
    /// </summary>
    public double Progress { get; private set; } = 0.0;


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>
    /// Get source list of <see cref="DisplayableLog"/> to be processed.
    /// </summary>
    public IList<DisplayableLog> SourceLogs { get; }


    // Start processing logs.
    void StartProcessingLogs()
    {
        // check state
        if (this.IsDisposed)
            return;

        // cancel current processing
        this.CancelProcessing();

        // create token
        var processingToken = this.CreateProcessingToken(out var isProcessingNeeded);

        // no need to process
        if (!isProcessingNeeded)
        {
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

        // start processing
        if (!this.IsProcessingNeeded)
        {
            this.IsProcessingNeeded = true;
            this.OnPropertyChanged(nameof(IsProcessingNeeded));
        }
        this.currentProcessingParams = new(processingToken);
        this.unprocessedLogs.AddAll(this.SourceLogs.Reverse(), true);
        for (var i = this.MaxConcurrencyLevel; i > 0; --i)
            this.ProcessNextChunk(this.currentProcessingParams);
    }
}