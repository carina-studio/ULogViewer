using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Processor to generate series of log chart.
/// </summary>
class DisplayableLogChartSeriesGenerator : BaseDisplayableLogProcessor<DisplayableLogChartSeriesGenerator.ProcessingToken, DisplayableLogChartSeriesGenerator.ProcessingResult>
{
    /// <summary>
    /// Result of processing of logs.
    /// </summary>
    public class ProcessingResult
    { }
    
    
    /// <summary>
    /// Token of processing.
    /// </summary>
    public class ProcessingToken
    {
        public static readonly ProcessingToken Empty = new();
    }
    
    
    // Type of values of series.
    enum SeriesValueType
    {
        Undefined,
        Value,
        ValueStatistic,
    }


    // Static fields.
    static readonly long SeriesValueMemorySize = Memory.EstimateInstanceSize<DisplayableLogChartSeriesValue>();
    static readonly Dictionary<LogChartType, SeriesValueType> SeriesValueTypes = new ()
    {
        { LogChartType.ValueStatisticBars, SeriesValueType.ValueStatistic },
        { LogChartType.ValueAreas, SeriesValueType.Value },
        { LogChartType.ValueAreasWithDataPoints, SeriesValueType.Value },
        { LogChartType.ValueBars, SeriesValueType.Value },
        { LogChartType.ValueCurves, SeriesValueType.Value },
        { LogChartType.ValueCurvesWithDataPoints, SeriesValueType.Value },
        { LogChartType.ValueLines, SeriesValueType.Value },
        { LogChartType.ValueLinesWithDataPoints, SeriesValueType.Value },
        { LogChartType.ValueStackedAreas, SeriesValueType.Value },
        { LogChartType.ValueStackedAreasWithDataPoints, SeriesValueType.Value },
        { LogChartType.ValueStackedBars, SeriesValueType.Value },
    };
    static readonly long ValueStatisticEntryMemorySize = Memory.EstimateInstanceSize<KeyValuePair<string, (int, int)>>();
    
    
    // Fields.
    bool isMaxTotalSeriesValueCountReached;
    IList<DisplayableLogChartSeriesSource> logChartSeriesSources = Array.Empty<DisplayableLogChartSeriesSource>();
    LogChartType logChartType = LogChartType.None;
    int maxSeriesValueCount;
    readonly ObservableList<DisplayableLogChartSeries> series = new();
    ObservableList<DisplayableLogChartSeriesValue?>[] seriesValues = [];
    SeriesValueType seriesValueType = SeriesValueType.Undefined;
    int totalSeriesValueCount;
    Dictionary<string, (int /* count */, int /* index */)>[] valueStatistics = [];
    long valueStatisticStringsMemorySize;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogChartSeriesGenerator"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparer"><see cref="IDisplayableLogComparer"/> which used on <paramref name="sourceLogs"/>.</param>
    public DisplayableLogChartSeriesGenerator(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, IDisplayableLogComparer comparer) : base(app, sourceLogs, comparer, DisplayableLogProcessingPriority.Default)
    {
        // setup collections
        this.Series = ListExtensions.AsReadOnly(this.series);
    }


    /// <inheritdoc/>
    protected override ProcessingToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        isProcessingNeeded = false;
        return ProcessingToken.Empty;
    }
    
    /// <summary>
    /// Check whether <see cref="TotalSeriesValueCount"/> reaches the limitation or not.
    /// </summary>
    public bool IsMaxTotalSeriesValueCountReached => this.isMaxTotalSeriesValueCountReached;


    /// <summary>
    /// Get or set list of <see cref="DisplayableLogChartSeriesSource"/> to generate series.
    /// </summary>
    public IList<DisplayableLogChartSeriesSource> LogChartSeriesSources
    {
        get => this.logChartSeriesSources;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.logChartSeriesSources.SequenceEqual(value))
                return;
            this.logChartSeriesSources = new List<DisplayableLogChartSeriesSource>(value).Also(it =>
            {
                var maxCount = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.MaxSeriesCountInLogChart);
                if (it.Count > maxCount)
                    it.RemoveRange(maxCount, it.Count - maxCount);
            }).AsReadOnly();
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(LogChartSeriesSources));
        }
    }


    /// <summary>
    /// Get or set type of chart.
    /// </summary>
    public LogChartType LogChartType
    {
        get => this.logChartType;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.logChartType == value)
                return;
            SeriesValueTypes.TryGetValue(this.logChartType, out var prevSeriesValueType);
            SeriesValueTypes.TryGetValue(value, out var seriesValueType);
            this.logChartType = value;
            if (prevSeriesValueType != seriesValueType)
                this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(LogChartType));
        }
    }


    /// <summary>
    /// Get maximum number of values in all series.
    /// </summary>
    public int MaxSeriesValueCount => this.maxSeriesValueCount;


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize
                                       + this.valueStatistics.Let(it =>
                                       {
                                           var memorySize = Memory.EstimateCollectionInstanceSize(ValueStatisticEntryMemorySize, it.Length);
                                           return memorySize + this.valueStatisticStringsMemorySize;
                                       })
                                       + this.seriesValues.Let(it =>
                                       {
                                           var memorySize = Memory.EstimateArrayInstanceSize(IntPtr.Size, it.Length);
                                           foreach (var values in it)
                                           {
                                               memorySize += Memory.EstimateCollectionInstanceSize(IntPtr.Size, values.Capacity);
                                               memorySize += SeriesValueMemorySize * values.Count;
                                           }
                                           return memorySize;
                                       });


    /// <inheritdoc/>
    protected override void OnChunkProcessed(ProcessingToken token, List<DisplayableLog> logs, List<ProcessingResult> results)
    { }


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log) => true;


    /// <inheritdoc/>
    protected override bool OnProcessLog(ProcessingToken token, DisplayableLog log, out ProcessingResult result)
    {
        result = new();
        return false;
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(ProcessingToken token, bool willStartProcessing)
    {
        this.series.Clear();
        this.valueStatistics = [];
        this.valueStatisticStringsMemorySize = 0L;
        this.seriesValues = [];
        this.maxSeriesValueCount = 0;
        this.OnPropertyChanged(nameof(MaxSeriesValueCount));
        this.totalSeriesValueCount = 0;
        this.OnPropertyChanged(nameof(TotalSeriesValueCount));
        if (this.isMaxTotalSeriesValueCountReached)
        {
            this.isMaxTotalSeriesValueCountReached = false;
            this.OnPropertyChanged(nameof(IsMaxTotalSeriesValueCountReached));
        }
    }


    /// <summary>
    /// Series of log chart.
    /// </summary>
    public IList<DisplayableLogChartSeries> Series { get; }
    
    
    /// <summary>
    /// Get total number of values in all series.
    /// </summary>
    public int TotalSeriesValueCount => this.totalSeriesValueCount;
}


/// <summary>
/// Single series of log chart.
/// </summary>
class DisplayableLogChartSeries : INotifyPropertyChanged
{
    // Fields.
    DisplayableLogChartSeriesValue? maxValue;
    DisplayableLogChartSeriesValue? minValue;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogChartSeries"/> instance.
    /// </summary>
    /// <param name="source">Source to generate the series.</param>
    /// <param name="values">List of values in series.</param>
    public DisplayableLogChartSeries(DisplayableLogChartSeriesSource? source, IList<DisplayableLogChartSeriesValue?> values)
    {
        this.Source = source;
        this.Values = ListExtensions.AsReadOnly(values);
        if (values is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged += this.OnValuesChanged;
        this.UpdateMinMaxValues();
    }


    // Get maximum value from values.
    static DisplayableLogChartSeriesValue? GetMaxValue(IList<DisplayableLogChartSeriesValue?> values)
    {
        var maxValue = default(DisplayableLogChartSeriesValue);
        for (var i = values.Count - 1; i >= 0; --i)
        {
            var value = values[i];
            if (value is null)
                continue;
            if (double.IsFinite(value.Value))
            {
                if (maxValue is null || maxValue.Value < value.Value)
                    maxValue = value;
            }
        }
        return maxValue;
    }
    
    
    // Get minimum value from values.
    static DisplayableLogChartSeriesValue? GetMinValue(IList<DisplayableLogChartSeriesValue?> values)
    {
        var minValue = default(DisplayableLogChartSeriesValue);
        for (var i = values.Count - 1; i >= 0; --i)
        {
            var value = values[i];
            if (value is null)
                continue;
            if (double.IsFinite(value.Value))
            {
                if (minValue is null || minValue.Value > value.Value)
                    minValue = value;
            }
        }
        return minValue;
    }


    /// <summary>
    /// Maximum value in <see cref="Values"/>.
    /// </summary>
    public DisplayableLogChartSeriesValue? MaxValue => this.maxValue;
    
    
    /// <summary>
    /// Minimum value in <see cref="Values"/>.
    /// </summary>
    public DisplayableLogChartSeriesValue? MinValue => this.maxValue;


    // Called when values changed.
    void OnValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.UpdateMinMaxValues(e.NewItems!.Cast<DisplayableLogChartSeriesValue?>());
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogChartSeriesValue?>().Let(removedValues =>
                {
                    if (this.minValue is not null && removedValues.Contains(this.minValue))
                        this.minValue = null;
                    if (this.maxValue is not null && removedValues.Contains(this.maxValue))
                        this.maxValue = null;
                    if (this.minValue is null || this.maxValue is null)
                        this.UpdateMinMaxValues();
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                this.minValue = null;
                this.maxValue = null;
                this.UpdateMinMaxValues();
                break;
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<DisplayableLogChartSeriesValue?>().Let(replacedValues =>
                {
                    if (this.minValue is not null && replacedValues.Contains(this.minValue))
                        this.minValue = null;
                    if (this.maxValue is not null && replacedValues.Contains(this.maxValue))
                        this.maxValue = null;
                    if (this.minValue is null || this.maxValue is null)
                        this.UpdateMinMaxValues();
                    else
                        this.UpdateMinMaxValues(e.NewItems!.Cast<DisplayableLogChartSeriesValue?>());
                });
                break;
        }
    }


    /// <inheritdoc cref="INotifyPropertyChanged.PropertyChanged"/>.
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>
    /// Source of series.
    /// </summary>
    public DisplayableLogChartSeriesSource? Source { get; }
    
    
    // Update min/max values.
    void UpdateMinMaxValues() =>
        this.UpdateMinMaxValues(Array.Empty<DisplayableLogChartSeriesValue?>());
    void UpdateMinMaxValues(IList<DisplayableLogChartSeriesValue?> newValues)
    {
        // minimum
        if (this.minValue is null)
        {
            this.minValue = GetMinValue(this.Values);
            if (this.minValue is not null)
                this.PropertyChanged?.Invoke(this, new(nameof(MinValue)));
        }
        else
        {
            var localMinValue = GetMinValue(newValues);
            if (localMinValue is not null 
                && double.IsFinite(localMinValue.Value) 
                && this.minValue.Value > localMinValue.Value)
            {
                this.minValue = localMinValue;
                this.PropertyChanged?.Invoke(this, new(nameof(MinValue)));
            }
        }
        
        // maximum
        if (this.maxValue is null)
        {
            this.maxValue = GetMaxValue(this.Values);
            if (this.maxValue is not null)
                this.PropertyChanged?.Invoke(this, new(nameof(MaxValue)));
        }
        else
        {
            var localMaxValue = GetMaxValue(newValues);
            if (localMaxValue is not null 
                && double.IsFinite(localMaxValue.Value) 
                && this.maxValue.Value < localMaxValue.Value)
            {
                this.maxValue = localMaxValue;
                this.PropertyChanged?.Invoke(this, new(nameof(MaxValue)));
            }
        }
    }
    
    
    /// <summary>
    /// List of values in series.
    /// </summary>
    public IList<DisplayableLogChartSeriesValue?> Values { get; }
}


/// <summary>
/// Single value of series of log chart.
/// </summary>
/// <param name="Log">Related log.</param>
/// <param name="Value">Value.</param>
/// <param name="Label">Label.</param>
record DisplayableLogChartSeriesValue(DisplayableLog? Log, double Value, string? Label = null);