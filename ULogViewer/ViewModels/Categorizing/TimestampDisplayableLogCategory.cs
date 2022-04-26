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
    public TimestampDisplayableLogCategory(IDisplayableLogCategorizer<TimestampDisplayableLogCategory> categorizer, DisplayableLog? log, DateTime timestamp) : base(categorizer, log)
    {
        this.Timestamp = timestamp;
    }


    /// <inheritdoc/>
    public override long MemorySize { get => base.MemorySize + 8; }


    /// <summary>
    /// Get timestamp of this category.
    /// </summary>
    public DateTime Timestamp { get; }
}