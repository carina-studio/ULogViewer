using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analysis result of <see cref="DisplayableLog"/>.
/// </summary>
abstract class DisplayableLogAnalysisResult : ICloneable, IEquatable<DisplayableLogAnalysisResult>
{
    // Constants.
    static readonly long DefaultMemorySize = 5 * IntPtr.Size;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalysisResult"/> instance.
    /// </summary>
    /// <param name="message">Message.</param>
    public DisplayableLogAnalysisResult(string? message)
    {
        this.LinkedListNode = new(this);
        this.Message = message;
    }


    /// <summary>
    /// Clone the result.
    /// </summary>
    /// <returns>Clone result.</returns>
    public abstract DisplayableLogAnalysisResult Clone();


    /// <inheritdoc/>
    public virtual bool Equals(DisplayableLogAnalysisResult? result) =>
        result != null
        && result.GetType() == this.GetType()
        && result.Message == this.Message;


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is DisplayableLogAnalysisResult result && this.Equals(result);


    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.Message?.GetHashCode() ?? (int)this.MemorySize;


    /// <summary>
    /// List node of the result.
    /// </summary>
    public LinkedListNode<DisplayableLogAnalysisResult> LinkedListNode { get; }


    // Interface implementations.
    object ICloneable.Clone() => this.Clone();


    /// <summary>
    /// Get memory size of the result instance in bytes.
    /// </summary>
    public virtual long MemorySize { get => DefaultMemorySize; }


    /// <summary>
    /// Get message of result.
    /// </summary>
    public string? Message { get; }


    /// <inheritdoc/>
    public override string ToString() =>
        this.Message ?? this.GetType().Name;
}