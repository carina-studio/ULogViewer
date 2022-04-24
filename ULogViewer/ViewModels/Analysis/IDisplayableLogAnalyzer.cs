using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analyzer of list of <see cref="DisplayableLog"/>.
/// </summary>
interface IDisplayableLogAnalyzer : IDisplayableLogProcessor
{
    /// <summary>
    /// Get results of analysis.
    /// </summary>
    IList<DisplayableLogAnalysisResult> AnalysisResults { get; }
}