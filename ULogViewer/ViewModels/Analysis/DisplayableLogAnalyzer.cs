using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base class of analyzer of <see cref="DisplayableLog"/>.
/// </summary>
abstract class DisplayableLogAnalyzer<TProcessingToken> : DisplayableLogProcessor<TProcessingToken, DisplayableLogAnalysisResult>, IDisplayableLogAnalyzer where TProcessingToken : class
{
    // Fields.
    readonly SortedObservableList<DisplayableLogAnalysisResult> analysisResults;
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
        this.analysisResults = new((lhs, rhs) => this.SourceLogComparison(lhs.Log, rhs.Log));
        this.AnalysisResults = this.analysisResults.AsReadOnly();
    }


    /// <inheritdoc/>
    public IList<DisplayableLogAnalysisResult> AnalysisResults { get; }


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize + this.analysisResultsMemorySize + this.analysisResults.Count * IntPtr.Size + 4;


    /// <inheritdoc/>
    protected override void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<DisplayableLogAnalysisResult> results)
    {
        for ( var i = results.Count - 1; i >= 0; --i)
            this.analysisResultsMemorySize += results[i].MemorySize;
        this.analysisResults.AddAll(results, true);
    }


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log)
    {
        var index = this.analysisResults.BinarySearch<DisplayableLogAnalysisResult, DisplayableLog>(log, it => it.Log, this.SourceLogComparison);
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
}