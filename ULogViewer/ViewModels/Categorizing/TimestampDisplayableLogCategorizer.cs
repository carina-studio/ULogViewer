using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels.Categorizing;

/// <summary>
/// <see cref="IDisplayableLogAnalyzer"/> to categorize logs by timestamp.
/// </summary>
class TimestampDisplayableLogCategorizer : BaseDisplayableLogCategorizer<TimestampDisplayableLogCategorizer.ProcessingToken, TimestampDisplayableLogCategory>
{
    /// <summary>
    /// Internal processing token.
    /// </summary>
    public class ProcessingToken
    {
        // Fields.
        public readonly TimestampDisplayableLogCategoryGranularity Granularity;
        public readonly Dictionary<DateTime, TimestampDisplayableLogCategory> PartialCategories = new();
        public readonly Func<DisplayableLog, DateTime?> TimestampGetter;

        // Constructor.
        public ProcessingToken() : this(new(log => null), default)
        { }
        public ProcessingToken(Func<DisplayableLog, DateTime?> timestampGetter, TimestampDisplayableLogCategoryGranularity granularity)
        {
            this.Granularity = granularity;
            this.TimestampGetter = timestampGetter;
        }
    }


    // Fields.
    readonly Dictionary<DateTime, TimestampDisplayableLogCategory> categoriesByTimestamp = new();
    readonly TimestampDisplayableLogCategory emptyCategory;
    TimestampDisplayableLogCategoryGranularity granularity = TimestampDisplayableLogCategoryGranularity.Day;
    string? timestampLogPropertyName;


    /// <summary>
    /// Initialize new <see cref="TimestampDisplayableLogCategorizer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public TimestampDisplayableLogCategorizer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.emptyCategory = new(this, null, DateTime.MinValue, default);
        this.Application.StringsUpdated += this.OnAppStringsUpdated;
    }


    /// <inheritdoc/>
    protected override ProcessingToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        // check log property
        isProcessingNeeded = false;
        if (this.timestampLogPropertyName == null)
            return new();
        
        // create property getter
        Func<DisplayableLog, DateTime?> getter;
        TimestampDisplayableLogCategoryGranularity granularity = this.granularity;
        DateTime QuantizeTimestamp(DateTime timestamp) => granularity switch
        {
            TimestampDisplayableLogCategoryGranularity.Hour => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0),
            TimestampDisplayableLogCategoryGranularity.Day => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0),
            _ => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0),
        };
        if (DisplayableLog.HasInt64Property(this.timestampLogPropertyName))
        {
            var binaryTimestampGetter = DisplayableLog.CreateLogPropertyGetter<long>(this.timestampLogPropertyName);
            getter = log =>
            {
                var binaryTimestamp = binaryTimestampGetter(log);
                if (binaryTimestamp == 0)
                    return null;
                return QuantizeTimestamp(DateTime.FromBinary(binaryTimestamp));
            };
        }
        else if (DisplayableLog.HasDateTimeProperty(this.timestampLogPropertyName))
        {
            var rawTimestampGetter = DisplayableLog.CreateLogPropertyGetter<DateTime?>(this.timestampLogPropertyName);
            getter = log =>
            {
                var rawTimestamp = rawTimestampGetter(log);
                if (!rawTimestamp.HasValue)
                    return null;
                return QuantizeTimestamp(rawTimestamp.Value);
            };
        }
        else
            return new();
        
        // create token
        isProcessingNeeded = true;
        return new(getter, granularity);
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.Application.StringsUpdated -= this.OnAppStringsUpdated;
        base.Dispose(disposing);
    }


    /// <summary>
    /// Get or set granularity to categorizing logs.
    /// </summary>
    public TimestampDisplayableLogCategoryGranularity Granularity
    {
        get => this.granularity;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.granularity == value)
                return;
            this.granularity = value;
            this.OnPropertyChanged(nameof(Granularity));
            this.InvalidateProcessing();
        }
    }


    // Called when application string resources updated.
    void OnAppStringsUpdated(object? sender, EventArgs e) =>
        this.InvalidateCategoryNames();


    /// <inheritdoc/>
    protected override void OnChunkProcessed(ProcessingToken token, List<DisplayableLog> logs, List<TimestampDisplayableLogCategory> results)
    {
        for (var i = logs.Count - 1; i >= 0; --i)
        {
            var timestamp = results[i].Timestamp;
            if (this.categoriesByTimestamp.TryGetValue(timestamp, out var existingResult) && existingResult != null)
            {
                if (this.CompareSourceLogs(existingResult.Log, logs[i]) > 0)
                    this.RemoveCategory(existingResult);
                else
                {
                    logs.RemoveAt(i);
                    results.RemoveAt(i);
                    continue;
                }
            }
            this.categoriesByTimestamp[timestamp] = results[i];
        }
        base.OnChunkProcessed(token, logs, results);
    }


    /// <inheritdoc/>
    protected override void OnLogProfilePropertyChanged(LogProfile profile, PropertyChangedEventArgs e)
    {
        base.OnLogProfilePropertyChanged(profile, e);
        if (e.PropertyName == nameof(LogProfile.TimestampFormatForDisplaying))
            this.InvalidateCategoryNames();
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(ProcessingToken token, bool willStartProcessing)
    {
        this.categoriesByTimestamp.Clear();
        if (!willStartProcessing && this.MemoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
            this.categoriesByTimestamp.TrimExcess();
        base.OnProcessingCancelled(token, willStartProcessing);
    }


    /// <inheritdoc/>
    protected override bool OnProcessLog(ProcessingToken token, DisplayableLog log, out TimestampDisplayableLogCategory result)
    {
        // get timestamp
        result = this.emptyCategory;
        var timestamp = token.TimestampGetter(log);
        if (!timestamp.HasValue)
            return false;
        
        // categorize
        lock (token.PartialCategories)
        {
            if (token.PartialCategories.TryGetValue(timestamp.Value, out var existingResult) 
                && existingResult != null
                && this.CompareSourceLogs(existingResult.Log, log) <= 0)
            {
                return false;
            }
            result = new(this, log, timestamp.Value, token.Granularity);
            token.PartialCategories[timestamp.Value] = result;
        }
        return true;
    }


    /// <summary>
    /// Get or set name of property of <see cref="DisplayableLog"/> to categorize timestamp of logs.
    /// </summary>
    public string? TimestampLogPropertyName
    {
        get => this.timestampLogPropertyName;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.timestampLogPropertyName == value)
                return;
            this.timestampLogPropertyName = value;
            this.OnPropertyChanged(nameof(TimestampLogPropertyName));
            this.InvalidateProcessing();
        }
    }
}