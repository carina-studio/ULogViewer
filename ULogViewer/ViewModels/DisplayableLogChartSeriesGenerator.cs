using CarinaStudio.Collections;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
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
        Category,
    }


    // Static fields.
    static readonly long SeriesCategoriesEntryMemorySize = Memory.EstimateInstanceSize<KeyValuePair<string, (int, int)>>();
    static readonly long SeriesValueMemorySize = Memory.EstimateInstanceSize<DisplayableLogChartSeriesValue>();
    static readonly Dictionary<LogChartType, SeriesValueType> SeriesValueTypes = new ()
    {
        { LogChartType.CategoryBars, SeriesValueType.Category },
        { LogChartType.ValueBars, SeriesValueType.Value },
        { LogChartType.ValueLines, SeriesValueType.Value },
        { LogChartType.ValueLinesWithDataPoints, SeriesValueType.Value },
        { LogChartType.ValueStackedAreas, SeriesValueType.Value },
        { LogChartType.ValueStackedAreasWithDataPoints, SeriesValueType.Value },
        { LogChartType.ValueStackedBars, SeriesValueType.Value },
    };


    // Fields.
    bool isMaxTotalSeriesValueCountReached;
    IList<DisplayableLogProperty> logChartProperties = Array.Empty<DisplayableLogProperty>();
    LogChartType logChartType = LogChartType.None;
    DisplayableLogChartSeriesValue? knownMaxSeriesValue;
    DisplayableLogChartSeriesValue? knownMinSeriesValue;
    int maxSeriesValueCount;
    readonly ScheduledAction reportMinMaxValuesAction;
    readonly ObservableList<DisplayableLogChartSeries> series = new();
    Dictionary<string, (int /* counter */, int /* index */)>[] seriesCategories = Array.Empty<Dictionary<string, (int, int)>>();
    long seriesCategoriesMemorySize;
    ObservableList<DisplayableLogChartSeriesValue>[] seriesValues = Array.Empty<ObservableList<DisplayableLogChartSeriesValue>>();
    SeriesValueType seriesValueType = SeriesValueType.Undefined;
    int totalSeriesValueCount;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogChartSeriesGenerator"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public DisplayableLogChartSeriesGenerator(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison, DisplayableLogProcessingPriority.Default)
    {
        // setup collections
        this.Series = ListExtensions.AsReadOnly(this.series);
        
        // setup actions
        this.reportMinMaxValuesAction = new(this.ReportMinMaxSeriesValues);
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

    // Check whether type of log chart is stacked chart or not.
    static bool IsStackedLogChartType(LogChartType type) => type switch
    {
        LogChartType.ValueStackedAreas
            or LogChartType.ValueStackedAreasWithDataPoints
            or LogChartType.ValueStackedBars => true,
        _ => false,
    };


    /// <summary>
    /// Get or set list of <see cref="DisplayableLogProperty"/> to generate series.
    /// </summary>
    public IList<DisplayableLogProperty> LogChartProperties
    {
        get => this.logChartProperties;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.logChartProperties.SequenceEqual(value))
                return;
            this.logChartProperties = new List<DisplayableLogProperty>(value).AsReadOnly();
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(LogChartProperties));
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
            var isPrevStackedLogChartType = IsStackedLogChartType(this.logChartType);
            var isStackedLogChartType = IsStackedLogChartType(value);
            this.logChartType = value;
            if (prevSeriesValueType != seriesValueType)
                this.InvalidateProcessing();
            else if (isPrevStackedLogChartType != isStackedLogChartType)
            {
                this.knownMinSeriesValue = null;
                this.knownMaxSeriesValue = null;
                this.reportMinMaxValuesAction.Execute();
            }
            this.OnPropertyChanged(nameof(LogChartType));
        }
    }


    /// <summary>
    /// Get known maximum value of all series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MaxSeriesValue => null;


    /// <summary>
    /// Get maximum number of values in all series.
    /// </summary>
    public int MaxSeriesValueCount => this.maxSeriesValueCount;


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize
                                       + this.seriesCategories.Let(it =>
                                       {
                                           var memorySize = Memory.EstimateCollectionInstanceSize(SeriesCategoriesEntryMemorySize, it.Length);
                                           return memorySize + this.seriesCategoriesMemorySize;
                                       })
                                       + this.seriesValues.Let(it =>
                                       {
                                           var memorySize = Memory.EstimateArrayInstanceSize(IntPtr.Size, it.Length);
                                           foreach (var values in it)
                                               memorySize += Memory.EstimateCollectionInstanceSize(SeriesValueMemorySize, values.Capacity);
                                           return memorySize;
                                       });
    
    
    /// <summary>
    /// Get known minimum value of all series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MinSeriesValue => null;


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
        this.seriesCategories = Array.Empty<Dictionary<string, (int, int)>>();
        this.seriesCategoriesMemorySize = 0L;
        this.seriesValues = Array.Empty<ObservableList<DisplayableLogChartSeriesValue>>();
        this.maxSeriesValueCount = 0;
        this.OnPropertyChanged(nameof(MaxSeriesValueCount));
        this.totalSeriesValueCount = 0;
        this.OnPropertyChanged(nameof(TotalSeriesValueCount));
        this.knownMinSeriesValue = null;
        this.knownMaxSeriesValue = null;
        this.reportMinMaxValuesAction.Execute();
        if (this.isMaxTotalSeriesValueCountReached)
        {
            this.isMaxTotalSeriesValueCountReached = false;
            this.OnPropertyChanged(nameof(IsMaxTotalSeriesValueCountReached));
        }
    }


    // Report latest min/max values of series.
    void ReportMinMaxSeriesValues()
    { }


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
class DisplayableLogChartSeries
{
    /// <summary>
    /// Initialize new <see cref="DisplayableLogChartSeries"/> instance.
    /// </summary>
    /// <param name="property">Property of log to generate the series.</param>
    /// <param name="values">List of values in series.</param>
    public DisplayableLogChartSeries(DisplayableLogProperty? property, IList<DisplayableLogChartSeriesValue> values)
    {
        this.LogProperty = property;
        this.Values = ListExtensions.AsReadOnly(values);
    }
    
    
    /// <summary>
    /// Property of log to generate the series.
    /// </summary>
    public DisplayableLogProperty? LogProperty { get; }
    
    
    /// <summary>
    /// List of values in series.
    /// </summary>
    public IList<DisplayableLogChartSeriesValue> Values { get; }
}


/// <summary>
/// Single value of series of log chart.
/// </summary>
/// <param name="Log">Related log.</param>
/// <param name="Value">Value.</param>
/// <param name="Label">Label.</param>
record struct DisplayableLogChartSeriesValue(DisplayableLog? Log, double? Value, string? Label = null);