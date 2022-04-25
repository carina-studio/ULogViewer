using System;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analysis result of <see cref="DisplayableLog"/>.
/// </summary>
class DisplayableLogAnalysisResult : IEquatable<DisplayableLogAnalysisResult>, INotifyPropertyChanged
{
    // Constants.
    static readonly long DefaultMemorySize = 3 * IntPtr.Size;


    // Fields.
    string? message;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalysisResult"/> instance.
    /// </summary>
    /// <param name="analyzer"><see cref="IDisplayableLogAnalyzer"/> which generates this result.</param>
    /// <param name="log"><see cref="DisplayableLog"/> which relates to this result.</param>
    /// <param name="message">Message.</param>
    public DisplayableLogAnalysisResult(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer, DisplayableLog? log, string? message = null)
    {
        this.Analyzer = analyzer;
        this.Log = log;
        this.MemorySize = DefaultMemorySize + (message != null ? message.Length * 2 + 4 : 0);
        this.message = message;
    }


    /// <summary>
    /// Get <see cref="IDisplayableLogAnalyzer"/> which generates this result.
    /// </summary>
    public IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> Analyzer { get; }


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
    public DisplayableLog? Log { get; }


    /// <summary>
    /// Get memory size of the result instance in bytes.
    /// </summary>
    public virtual long MemorySize { get; }


    /// <summary>
    /// Get or set message of result.
    /// </summary>
    public string? Message
    { 
        get => this.message; 
        protected set
        {
            if (this.message == value)
                return;
            this.message = value;
            this.OnPropertyChanged(nameof(Message));
        }
    }


    /// <summary>
    /// Raise <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    protected virtual void OnPropertyChanged(string propertyName) => 
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <inheritdoc/>
    public override string ToString() =>
        this.Message ?? this.GetType().Name;
}