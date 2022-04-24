using System;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analysis result of <see cref="DisplayableLog"/>.
/// </summary>
class DisplayableLogAnalysisResult : IEquatable<DisplayableLogAnalysisResult>
{
    // Constants.
    static readonly long DefaultMemorySize = 3 * IntPtr.Size;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalysisResult"/> instance.
    /// </summary>
    /// <param name="analyzer">Get <see cref="IDisplayableLogAnalyzer"/> which generates this result.</param>
    /// <param name="log"><see cref="DisplayableLog"/> which relates to this result.</param>
    /// <param name="message">Message.</param>
    public DisplayableLogAnalysisResult(IDisplayableLogAnalyzer analyzer, DisplayableLog log, string? message = null)
    {
        this.Analyzer = analyzer;
        this.Log = log;
        this.MemorySize = DefaultMemorySize + (message != null ? message.Length * 2 + 4 : 0);
        this.Message = message;
    }


    /// <summary>
    /// Get <see cref="IDisplayableLogAnalyzer"/> which generates this result.
    /// </summary>
    public IDisplayableLogAnalyzer Analyzer { get; }


    /// <inheritdoc/>
    public virtual bool Equals(DisplayableLogAnalysisResult? result) =>
        result != null
        && result.Analyzer == this.Analyzer
        && result.Log == this.Log
        && result.GetType() == this.GetType()
        && result.Message == this.Message;


    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) =>
        obj is DisplayableLogAnalysisResult result && this.Equals(result);


    /// <inheritdoc/>
    public sealed override int GetHashCode() =>
        this.Message?.GetHashCode() ?? (int)this.MemorySize;
    

    /// <summary>
    /// Get <see cref="DisplayableLog"/> which relates to this result.
    /// </summary>
    public DisplayableLog Log { get; }


    /// <summary>
    /// Get memory size of the result instance in bytes.
    /// </summary>
    public virtual long MemorySize { get; }


    /// <summary>
    /// Get message of result.
    /// </summary>
    public string? Message { get; }


    /// <inheritdoc/>
    public override string ToString() =>
        this.Message ?? this.GetType().Name;
}