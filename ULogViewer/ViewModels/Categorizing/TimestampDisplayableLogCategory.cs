using System;

namespace CarinaStudio.ULogViewer.ViewModels.Categorizing;

/// <summary>
/// <see cref="DisplayableLogCategory"/> based-on timestamp of <see cref="DisplayableLog"/>.
/// </summary>
class TimestampDisplayableLogCategory : DisplayableLogCategory
{
    /// <summary>
    /// Initializer new <see cref="TimestampDisplayableLogCategory"/> instance.
    /// </summary>
    /// <param name="categorizer"><see cref="IDisplayableLogCategorizer{T}>"/> which generate this category.</param>
    /// <param name="log"><see cref="DisplayableLog"/> which represents this category.</param>
    /// <param name="timestamp">Timestamp of this category.</param>
    /// <param name="granularity">Granularity of category.</param>
    public TimestampDisplayableLogCategory(IDisplayableLogCategorizer<TimestampDisplayableLogCategory> categorizer, DisplayableLog? log, DateTime timestamp, TimestampDisplayableLogCategoryGranularity granularity) : base(categorizer, log)
    {
        this.Granularity = granularity;
        this.Timestamp = timestamp;
    }


    /// <summary>
    /// Get granularity of category.
    /// </summary>
    public TimestampDisplayableLogCategoryGranularity Granularity { get; }


    /// <inheritdoc/>
    public override long MemorySize { get => base.MemorySize + 8; }


    /// <inheritdoc/>
    protected override string? OnUpdateName()
    {
        var format = this.Log?.Group?.LogProfile?.TimestampFormatForDisplaying;
        if (format != null)
            return this.Timestamp.ToString(format);
        return this.Timestamp.ToLongTimeString();
    }


    /// <summary>
    /// Get timestamp of this category.
    /// </summary>
    public DateTime Timestamp { get; }
}


/// <summary>
/// Granularity of category of log by timestamp.
/// </summary>
enum TimestampDisplayableLogCategoryGranularity
{
    /// <summary>
    /// Minute.
    /// </summary>
    Minute,
    /// <summary>
    /// Hour.
    /// </summary>
    Hour,
    /// <summary>
    /// Day.
    /// </summary>
    Day,
}