using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analyzer of list of <see cref="DisplayableLog"/>.
/// </summary>
interface IDisplayableLogAnalyzer<out TResult> : IDisplayableLogProcessor where TResult : DisplayableLogAnalysisResult
{
    /// <summary>
    /// Get results of analysis.
    /// </summary>
    IReadOnlyList<TResult> AnalysisResults { get; }
}