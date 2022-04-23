using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base class of analyzer of <see cref="DisplayableLog"/>.
/// </summary>
abstract class DisplayableLogAnalyzer<TProcessingToken> : DisplayableLogProcessor<TProcessingToken, IList<DisplayableLogAnalysisResult>> where TProcessingToken : class
{
    /// <summary>
    /// Empty analysis result.
    /// </summary>
    protected static readonly IList<DisplayableLogAnalysisResult> EmptyResult = new DisplayableLogAnalysisResult[0];


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected DisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { }


    /// <inheritdoc/>
    protected override void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<IList<DisplayableLogAnalysisResult>> results)
    {
        //
    }
}