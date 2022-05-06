using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Dummy implementation of <see cref="IDisplayableLogAnalyzer{TResult}"/>.
/// </summary>
class DummyDisplayableLogAnalyzer : BaseDisplayableLogAnalyzer
{
    // Result.
    class Result : DisplayableLogAnalysisResult
    {
        // Constructor.
        public Result(DummyDisplayableLogAnalyzer analyzer, DisplayableLog log) : base(analyzer, Enum.GetValues<DisplayableLogAnalysisResultType>().SelectRandomElement(), log)
        { }

        // Update message.
        protected override string? OnUpdateMessage() =>
            $"Result #{this.Id}";
    }


    /// <summary>
    /// Initialize new <see cref="DummyDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public DummyDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { }


    /// <inheritdoc/>
    protected override bool CheckIsAnalyzingNeeded() =>
        true;
    

    /// <inheritdoc/>
    protected override void OnChunkProcessed(object token, List<DisplayableLog> logs, List<IList<DisplayableLogAnalysisResult>> results)
    {
        System.Diagnostics.Debug.WriteLine($"Analysis result count: {results.Count}");
        base.OnChunkProcessed(token, logs, results);
    }


    /// <inheritdoc/>
    protected override bool OnProcessLog(object token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        if ((log.Message?.Length).GetValueOrDefault() > 200)
        {
            result = new DisplayableLogAnalysisResult[] { new Result(this, log) };
            return true;
        }
        result = this.EmptyResults;
        return false;
    } 
}