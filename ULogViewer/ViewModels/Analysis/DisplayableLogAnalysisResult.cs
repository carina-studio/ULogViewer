using Avalonia.Media;
using CarinaStudio.Threading;
using System;
using System.ComponentModel;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analysis result of <see cref="DisplayableLog"/>.
/// </summary>
class DisplayableLogAnalysisResult : BaseApplicationObject<IULogViewerApplication>, INotifyPropertyChanged
{
    // Static fields.
    static readonly long DefaultMemorySize = (7 * IntPtr.Size) // Appliation, BeginningLog, EndingLog, Log, message, Analyzer, PropertyChanged
        + (4 + 8) // ByteSize
        + (4 + 8) // Duration
        + 4 // Id
        + 4 // isMessageValid
        + (4 + 8) // Quantity
        + 4; // Type
    static volatile int NextId = 1;


    // Fields.
    bool isMessageValid;
    string? message;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalysisResult"/> instance.
    /// </summary>
    /// <param name="analyzer"><see cref="IDisplayableLogAnalyzer"/> which generates this result.</param>
    /// <param name="type">Type of result.</param>
    /// <param name="log"><see cref="DisplayableLog"/> which relates to this result.</param>
    public DisplayableLogAnalysisResult(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer, DisplayableLogAnalysisResultType type, DisplayableLog? log) : base(analyzer.Application)
    {
        this.Analyzer = analyzer;
        this.Id = Interlocked.Increment(ref NextId);
        this.Log = log;
        this.Type = type;
    }


    /// <summary>
    /// Get <see cref="IDisplayableLogAnalyzer"/> which generates this result.
    /// </summary>
    public IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> Analyzer { get; }


    /// <summary>
    /// Get beginning <see cref="DisplayableLog"/> which relates to this result.
    /// </summary>
    public virtual DisplayableLog? BeginningLog { get; }


    /// <summary>
    /// Get size in bytes which is related to this result.
    /// </summary>
    public virtual long? ByteSize { get; }


    /// <summary>
    /// Get <see cref="IBrush"/> of color indicator.
    /// </summary>
    public IBrush? ColorIndicatorBrush 
    { 
        get => (this.Log ?? this.BeginningLog ?? this.EndingLog)?.ColorIndicatorBrush;
    }


    /// <summary>
    /// Get related duration of result.
    /// </summary>
    public virtual TimeSpan? Duration { get; }


    /// <summary>
    /// Get ending <see cref="DisplayableLog"/> which relates to this result.
    /// </summary>
    public virtual DisplayableLog? EndingLog { get; }


    /// <summary>
    /// Check whether <see cref="ByteSize"/> is valid value or not.
    /// </summary>
    public bool HasByteSize { get => this.ByteSize.HasValue; }


    /// <summary>
    /// Check whether <see cref="Duration"/> is valid value or not.
    /// </summary>
    public bool HasDuration { get => this.Duration.HasValue; }


    /// <summary>
    /// Check whether <see cref="Quantity"/> is valid value or not.
    /// </summary>
    public bool HasQuantity { get => this.Quantity.HasValue; }


    /// <summary>
    /// Get unique ID of result.
    /// </summary>
    public int Id { get; }


    /// <summary>
    /// Invalidate and update message of result.
    /// </summary>
    public void InvalidateMessage()
    {
        this.VerifyAccess();
        if (this.isMessageValid)
        {
            var message = this.OnUpdateMessage();
            if (this.message != message)
            {
                this.message = message;
                this.OnPropertyChanged(nameof(Message));
            }
        }
    }


    /// <summary>
    /// Check whether type of result is <see cref="DisplayableLogAnalysisResultType.Error"/> or not.
    /// </summary>
    public bool IsError { get => this.Type == DisplayableLogAnalysisResultType.Error; }


    /// <summary>
    /// Check whether type of result is <see cref="DisplayableLogAnalysisResultType.Information"/> or not.
    /// </summary>
    public bool IsInformation { get => this.Type == DisplayableLogAnalysisResultType.Information; }


    /// <summary>
    /// Check whether type of result is <see cref="DisplayableLogAnalysisResultType.Warning"/> or not.
    /// </summary>
    public bool IsWarning { get => this.Type == DisplayableLogAnalysisResultType.Warning; }
    

    /// <summary>
    /// Get <see cref="DisplayableLog"/> which relates to this result.
    /// </summary>
    public DisplayableLog? Log { get; }


    /// <summary>
    /// Get memory size of the result instance in bytes.
    /// </summary>
    public virtual long MemorySize { get => DefaultMemorySize; }


    /// <summary>
    /// Get message of result.
    /// </summary>
    public string? Message
    { 
        get
        {
            if (!this.CheckAccess())
                return this.message;
            if (!this.isMessageValid)
            {
                this.message = this.OnUpdateMessage();
                this.isMessageValid = true;
            }
            return this.message;
        }
    }


    /// <summary>
    /// Raise <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    protected virtual void OnPropertyChanged(string propertyName) => 
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    

    /// <summary>
    /// Called to update message of result.
    /// </summary>
    /// <returns>Message of result.</returns>
    protected virtual string? OnUpdateMessage() => null;


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>
    /// Get related quantity of analysis result.
    /// </summary>
    public virtual long? Quantity { get; }


    /// <inheritdoc/>
    public override string ToString() =>
        $"[{this.Type}]: {this.message}";


    /// <summary>
    /// Get type of result.
    /// </summary>
    public DisplayableLogAnalysisResultType Type { get; }
}


/// <summary>
/// Type of <see cref="DisplayableLogAnalysisResult"/>.
/// </summary>
enum DisplayableLogAnalysisResultType : uint
{
    /// <summary>
    /// Error.
    /// </summary>
    Error,
    /// <summary>
    /// Warning.
    /// </summary>
    Warning,
    /// <summary>
    /// Start of operation.
    /// </summary>
    OperationStart,
    /// <summary>
    /// End of operation.
    /// </summary>
    OperationEnd,
    /// <summary>
    /// Increase.
    /// </summary>
    Increase,
    /// <summary>
    /// Decrease.
    /// </summary>
    Decrease,
    /// <summary>
    /// Steady.
    /// </summary>
    Steady,
    /// <summary>
    /// Fast.
    /// </summary>
    Fast,
    /// <summary>
    /// Slow.
    /// </summary>
    Slow,
    /// <summary>
    /// Checkpoint.
    /// </summary>
    Checkpoint,
    /// <summary>
    /// Time span.
    /// </summary>
    TimeSpan,
    /// <summary>
    /// Performance.
    /// </summary>
    Performance,
    /// <summary>
    /// Frequency.
    /// </summary>
    Frequency,
    /// <summary>
    /// Trend.
    /// </summary>
    Trend,
    /// <summary>
    /// Information.
    /// </summary>
    Information,
    /// <summary>
    /// Skipped operation.
    /// </summary>
    SkippedOperation,
    /// <summary>
    /// Debug.
    /// </summary>
    Debug,
}