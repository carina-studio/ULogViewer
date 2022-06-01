using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Base class for contextual-based log analyzer.
/// </summary>
/// <typeparam name="TProcessingToken">Type of processing token.</typeparam>
abstract class ContextualBasedDisplayableLogAnalyzer<TProcessingToken> : BaseDisplayableLogAnalyzer<TProcessingToken> where TProcessingToken : ContextualBasedAnalysisContext
{
    /// <summary>
    /// Initialize new <see cref="ContextualBasedDisplayableLogAnalyzer{TProcessingToken}"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected ContextualBasedDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, sourceLogs, comparison, priority)
    { }
}