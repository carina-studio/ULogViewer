using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base class of analyzer of <see cref="DisplayableLog"/>.
/// </summary>
abstract class DisplayableLogAnalyzer<TProcessingToken, TResult> : DisplayableLogProcessor<TProcessingToken, TResult>, IDisplayableLogAnalyzer<TResult> where TProcessingToken : class where TResult : DisplayableLogAnalysisResult
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
    protected DisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { 
        this.analysisResults = new((lhs, rhs) => this.CompareSourceLogs(lhs.Log, rhs.Log));
        this.AnalysisResults = (IReadOnlyList<TResult>)this.analysisResults.AsReadOnly();
    }


    /// <inheritdoc/>
    public IReadOnlyList<TResult> AnalysisResults { get; }


    /// <summary>
    /// Compare nullable <see cref="DisplayableLog"/>s by <see cref="SourceLogComparison"/>.
    /// </summary>
    /// <param name="lhs">Left hand side log.</param>
    /// <param name="rhs">Right hand side log.</param>
    /// <returns>Comparison result.</returns>
    protected int CompareSourceLogs(DisplayableLog? lhs, DisplayableLog? rhs)
    {
        if (lhs != null)
        {
            if (rhs != null)
                return this.SourceLogComparison(lhs, rhs);
            return -1;
        }
        if (rhs != null)
            return 1;
        return 0;
    }


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize + this.analysisResultsMemorySize + this.analysisResults.Count * IntPtr.Size + 4;


    /// <inheritdoc/>
    protected override void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<TResult> results)
    {
        for ( var i = results.Count - 1; i >= 0; --i)
            this.analysisResultsMemorySize += results[i].MemorySize;
        this.analysisResults.AddAll(results, true);
    }


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log)
    {
        var index = this.analysisResults.BinarySearch<TResult, DisplayableLog?>(log, it => it.Log, this.CompareSourceLogs);
        if (index >= 0)
        {
            this.analysisResultsMemorySize -= this.analysisResults[index].MemorySize;
            this.analysisResults.RemoveAt(index);
        }
        return false;
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(TProcessingToken token)
    {
        this.analysisResultsMemorySize = 0L;
        this.analysisResults.Clear();
    }


    /// <summary>
    /// Remove existing analysis result directly.
    /// </summary>
    /// <param name="result">Result to be removed.</param>
    /// <returns>True if result has been removed successfully.</returns>
    protected bool RemoveAnalysisResult(TResult result) =>
        this.analysisResults.Remove(result);
}