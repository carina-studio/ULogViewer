using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.ViewModels.Categorizing;

/// <summary>
/// <see cref="DisplayableLogCategory"/> based-on timestamp of <see cref="DisplayableLog"/>.
/// </summary>
class TimestampDisplayableLogCategory : DisplayableLogCategory
{
    // Static fields.
    static readonly Dictionary<string, string> CachedTimestampFormatsDay = new();
    static readonly Dictionary<string, string> CachedTimestampFormatsHour = new();
    static readonly Dictionary<string, string> CachedTimestampFormatsMinute = new();
    static readonly Regex HoursFormatRegex = new("[\\s]*[^\\s\\w]*h{1,2}[^\\s\\w]*[\\s]*", RegexOptions.IgnoreCase);
    static readonly Regex MinutesFormatRegex = new("[\\s]*[^\\s\\w]*m{1,2}[^\\s\\w]*[\\s]*");
    static readonly Regex SecondsFormatRegex = new("[\\s]*[^\\s\\w]*s{1,2}[^\\s\\w]*[\\s]*", RegexOptions.IgnoreCase);
    static readonly Regex SubSecondsFormatRegex = new("[\\s]*[^\\s\\w]*f{1,7}[^\\s\\w]*[\\s]*", RegexOptions.IgnoreCase);


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


    // Get proper timestamp format.
    string GetTimestampFormat(string baseFormat)
    {
        // use cached format
        var format = (string?)null;
        var cache = this.Granularity switch
        {
            TimestampDisplayableLogCategoryGranularity.Day => CachedTimestampFormatsDay,
            TimestampDisplayableLogCategoryGranularity.Hour => CachedTimestampFormatsHour,
            TimestampDisplayableLogCategoryGranularity.Minute => CachedTimestampFormatsMinute,
            _ => null,
        };
        if (cache?.TryGetValue(baseFormat, out format) == true && format != null)
            return format;
        
        // remove sub-seconds part
        format = baseFormat;
        var formatBuilder = new StringBuilder(baseFormat);
        var match = SubSecondsFormatRegex.Match(format);
        if (match.Success)
        {
            formatBuilder.Remove(match.Index, match.Length);
            format = formatBuilder.ToString();
        }

        // remove seconds part
        match = SecondsFormatRegex.Match(format);
        if (match.Success)
        {
            formatBuilder.Remove(match.Index, match.Length);
            format = formatBuilder.ToString();
        }

        // remove minutes/hours part
        switch (this.Granularity)
        {
            case TimestampDisplayableLogCategoryGranularity.Day:
                match = HoursFormatRegex.Match(format);
                if (match.Success)
                {
                    formatBuilder.Remove(match.Index, match.Length);
                    format = formatBuilder.ToString();
                }
                match = MinutesFormatRegex.Match(format);
                if (match.Success)
                {
                    formatBuilder.Remove(match.Index, match.Length);
                    format = formatBuilder.ToString();
                }
                break;
        }

        // complete
        cache?.Add(baseFormat, format);
        return format;
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
        var format = this.Log?.Group?.LogProfile?.TimestampFormatForDisplaying?.Let(this.GetTimestampFormat);
        if (format != null)
            return this.Timestamp.ToString(format);
        return this.Granularity switch
        {
            TimestampDisplayableLogCategoryGranularity.Day => this.Timestamp.ToShortDateString(),
            _ => this.Timestamp.ToShortTimeString(),
        };
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