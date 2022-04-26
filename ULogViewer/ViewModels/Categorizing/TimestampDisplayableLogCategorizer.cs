using CarinaStudio.Threading;
using System;
using System.Collections.Generic;

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
        public readonly Dictionary<DateTime, TimestampDisplayableLogCategory> PartialCategories = new();
        public readonly Func<DisplayableLog, DateTime?> TimestampGetter;

        // Constructor.
        public ProcessingToken() : this(new(log => null))
        { }
        public ProcessingToken(Func<DisplayableLog, DateTime?> timestampGetter)
        {
            this.TimestampGetter = timestampGetter;
        }
    }


    // Fields.
    readonly Dictionary<DateTime, TimestampDisplayableLogCategory> categoriesByTimestamp = new();
    readonly TimestampDisplayableLogCategory emptyCategory;
    string? timestampLogPropertyName;


    /// <summary>
    /// Initialize new <see cref="TimestampDisplayableLogCategorizer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public TimestampDisplayableLogCategorizer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.emptyCategory = new(this, null, DateTime.MinValue);
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
        if (DisplayableLog.HasInt64Property(this.timestampLogPropertyName))
        {
            var binaryTimestampGetter = DisplayableLog.CreateLogPropertyGetter<long>(this.timestampLogPropertyName);
            getter = log =>
            {
                var binaryTimestamp = binaryTimestampGetter(log);
                if (binaryTimestamp == 0)
                    return null;
                var timestamp = DateTime.FromBinary(binaryTimestamp);
                return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0);
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
                var timestamp = rawTimestamp.Value;
                return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0);
            };
        }
        else
            return new();
        
        // create token
        isProcessingNeeded = true;
        return new(getter);
    }


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
    protected override void OnProcessingCancelled(ProcessingToken token)
    {
        this.categoriesByTimestamp.Clear();
        base.OnProcessingCancelled(token);
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
            result = new(this, log, timestamp.Value);
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