using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base implementation of <see cref="IDisplayableLogAnalyzer{TResult}"/>.
/// </summary>
abstract class BaseDisplayableLogAnalyzer<TProcessingToken, TResult> : BaseDisplayableLogProcessor<TProcessingToken, TResult>, IDisplayableLogAnalyzer<TResult> where TProcessingToken : class where TResult : DisplayableLogAnalysisResult
{
    // Fields.
    readonly SortedObservableList<TResult> analysisResults;
    long analysisResultsMemorySize;

    
    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected BaseDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { 
        this.analysisResults = new((lhs, rhs) => this.CompareSourceLogs(lhs.Log, rhs.Log));
        this.AnalysisResults = (IReadOnlyList<TResult>)this.analysisResults.AsReadOnly();
    }


    /// <inheritdoc/>
    public IReadOnlyList<TResult> AnalysisResults { get; }


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
            + (this.analysisResults.Count + 1) * IntPtr.Size // analysisResults
            + 8 // analysisResultsMemorySize
            + this.analysisResultsMemorySize;
    }


    /// <inheritdoc/>
    protected override void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<TResult> results)
    {
        if (results.IsEmpty())
            return;
        for (var i = results.Count - 1; i >= 0; --i)
            this.analysisResultsMemorySize += results[i].MemorySize;
        this.analysisResults.AddAll(results, true);
        this.OnPropertyChanged(nameof(MemorySize));
    }


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log)
    {
        var index = this.analysisResults.BinarySearch<TResult, DisplayableLog?>(log, it => it.Log, this.CompareSourceLogs);
        if (index >= 0)
        {
            this.analysisResultsMemorySize -= this.analysisResults[index].MemorySize;
            this.analysisResults.RemoveAt(index);
            this.OnPropertyChanged(nameof(MemorySize));
        }
        return false;
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(TProcessingToken token)
    {
        this.analysisResults.Clear();
        if (this.analysisResultsMemorySize != 0L)
        {
            this.analysisResultsMemorySize = 0L;
            this.OnPropertyChanged(nameof(MemorySize));
        }
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