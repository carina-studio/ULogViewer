using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base implementation of <see cref="IDisplayableLogAnalyzer{TResult}"/>.
/// </summary>
abstract class BaseDisplayableLogAnalyzer<TProcessingToken, TResult> : BaseDisplayableLogProcessor<TProcessingToken, IList<TResult>>, IDisplayableLogAnalyzer<TResult> where TProcessingToken : class where TResult : DisplayableLogAnalysisResult
{
    // Fields.
    readonly SortedObservableList<TResult> analysisResults;
    long analysisResultsMemorySize;
    readonly List<TResult> tempResults = new();

    
    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalyzer{TProcessingToken, TResult}"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected BaseDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { 
        this.analysisResults = new((lhs, rhs) => lhs.Id - rhs.Id);
        this.AnalysisResults = new Collections.SafeReadOnlyList<TResult>(this.analysisResults);
    }


    /// <inheritdoc/>
    public IReadOnlyList<TResult> AnalysisResults { get; }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.VerifyAccess();
            if (this.analysisResults.IsNotEmpty())
            {
                foreach (var log in this.SourceLogs)
                    log.RemoveAnalysisResults(this);
                this.analysisResults.Clear();
            }
        }
        base.Dispose(disposing);
    }


    /// <summary>
    /// Invalidate and update message of all analysis results.
    /// </summary>
    protected void InvalidateAnalysisResultMessages()
    {
        foreach (var result in this.analysisResults)
            result.InvalidateMessage();
    }


    /// <inheritdoc/>
    public override long MemorySize
    {
        get => base.MemorySize 
            + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.analysisResults.Count)
            + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.tempResults.Capacity)
            + this.analysisResultsMemorySize;
    }


    /// <inheritdoc/>
    protected override void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<IList<TResult>> results)
    {
        var count = logs.Count;
        if (count == 0)
            return;
        var sourceLogs = this.SourceLogs;
        for (var i = 0; i < count; ++i)
        {
            var resultList = results[i];
            for (var j = resultList.Count - 1; j >= 0; --j)
            {
                var result = resultList[j];
                var beginningLog = result.BeginningLog;
                var endingLog = result.EndingLog;
                var log = result.Log;
                if (beginningLog != null && !sourceLogs.Contains(beginningLog))
                    continue;
                if (endingLog != null && !sourceLogs.Contains(endingLog))
                    continue;
                if (log != null && !sourceLogs.Contains(log))
                    continue;
                this.analysisResultsMemorySize += result.MemorySize;
                this.tempResults.Add(result);
                log?.AddAnalysisResult(result);
                if (beginningLog != null && beginningLog != log)
                    beginningLog.AddAnalysisResult(result);
                if (endingLog != null && endingLog != log && endingLog != beginningLog)
                    endingLog.AddAnalysisResult(result);
            }
        }
        this.analysisResults.AddAll(this.tempResults);
        this.tempResults.Clear();
        this.OnPropertyChanged(nameof(MemorySize));
    }


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log)
    {
        var results = this.analysisResults;
        var index = results.BinarySearch<TResult, DisplayableLog?>(log, it => it.Log, this.CompareSourceLogs);
        if (index >= 0)
        {
            var startIndex = index;
            var endIndex = index + 1;
            var resultCount = results.Count;
            while (startIndex > 0 && results[startIndex - 1].Log == log)
                --startIndex;
            while (endIndex < resultCount && results[endIndex].Log == log)
                ++endIndex;
            for (var i = endIndex - 1; i >= startIndex; --i)
                this.analysisResultsMemorySize -= results[i].MemorySize;
            results.RemoveRange(startIndex, endIndex - startIndex);
            this.OnPropertyChanged(nameof(MemorySize));
        }
        return false;
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(TProcessingToken token, bool willStartProcessing)
    {
        if (this.analysisResults.IsNotEmpty())
        {
            foreach (var log in this.SourceLogs)
                log.RemoveAnalysisResults(this);
            this.analysisResults.Clear();
            if (!willStartProcessing && this.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy) != MemoryUsagePolicy.BetterPerformance)
                this.analysisResults.TrimExcess();
        }
        if (this.analysisResultsMemorySize != 0L)
        {
            this.analysisResultsMemorySize = 0L;
            this.OnPropertyChanged(nameof(MemorySize));
        }
    }


    /// <inheritdoc/>
    protected override void OnSourceLogsChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<DisplayableLog>().Let(oldItems =>
                {
                    foreach (var log in oldItems)
                    {
                        if (log.HasAnalysisResult)
                        {
                            var results = log.AnalysisResults;
                            for (var i = results.Count - 1; i >= 0; --i)
                            {
                                if (results[i].Analyzer == this)
                                    this.analysisResults.Remove((TResult)results[i]);
                            }
                        }
                    }
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                this.analysisResults.Clear();
                break;
        }
        base.OnSourceLogsChanged(e);
    }


    /// <summary>
    /// Remove existing analysis result directly.
    /// </summary>
    /// <param name="result">Result to be removed.</param>
    /// <returns>True if result has been removed successfully.</returns>
    protected bool RemoveAnalysisResult(TResult result)
    {
        if (this.analysisResults.Remove(result))
        {
            this.analysisResultsMemorySize -= result.MemorySize;
            this.OnPropertyChanged(nameof(MemorySize));
            return true;
        }
        return false;
    }
}


/// <summary>
/// Base implementation of <see cref="IDisplayableLogAnalyzer{TResult}"/>.
/// </summary>
abstract class BaseDisplayableLogAnalyzer<TProcessingToken> : BaseDisplayableLogAnalyzer<TProcessingToken, DisplayableLogAnalysisResult> where TProcessingToken : class
{
    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalyzer{TProcessingToken}"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected BaseDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { 
        this.EmptyResults = Array.Empty<DisplayableLogAnalysisResult>();
    }


    /// <summary>
    /// Get empty list of <see cref="DisplayableLogAnalysisResult"/>.
    /// </summary>
    protected IList<DisplayableLogAnalysisResult> EmptyResults { get; }
}


/// <summary>
/// Base implementation of <see cref="IDisplayableLogAnalyzer{TResult}"/>.
/// </summary>
abstract class BaseDisplayableLogAnalyzer : BaseDisplayableLogAnalyzer<object>
{
    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected BaseDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { }


    /// <summary>
    /// Called to check whether analyzing is needed or not.
    /// </summary>
    /// <returns>True if analyzing is needed.</returns>
    protected abstract bool CheckIsAnalyzingNeeded();


    /// <inheritdoc/>
    protected override object CreateProcessingToken(out bool isProcessingNeeded)
    {
        isProcessingNeeded = this.CheckIsAnalyzingNeeded();
        return new();
    }
}