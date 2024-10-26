using Avalonia.Data.Converters;
using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Data.Converters;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log chart.
/// </summary>
class LogChartViewModel : SessionComponent
{
    /// <summary>
    /// Minimum size of chart panel.
    /// </summary>
    public const double MinPanelSize = 140;
    
    
    /// <summary>
    /// Define <see cref="AreAllSeriesSourcesVisible"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> AreAllSeriesSourcesVisibleProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(AreAllSeriesSourcesVisible), true);
    /// <summary>
    /// Define <see cref="ChartType"/> property.
    /// </summary>
    public static readonly ObservableProperty<LogChartType> ChartTypeProperty = ObservableProperty.Register<LogChartViewModel, LogChartType>(nameof(ChartType), LogChartType.None);
    /// <summary>
    /// Define <see cref="ChartValueGranularity"/> property.
    /// </summary>
    public static readonly ObservableProperty<LogChartValueGranularity> ChartValueGranularityProperty = ObservableProperty.Register<LogChartViewModel, LogChartValueGranularity>(nameof(ChartValueGranularity), LogChartValueGranularity.Default);
    /// <summary>
    /// Define <see cref="IsChartDefined"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> IsChartDefinedProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(IsChartDefined), false);
    /// <summary>
    /// Define <see cref="IsPanelVisible"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> IsPanelVisibleProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(IsPanelVisible), false);
    /// <summary>
    /// Define <see cref="IsXAxisInverted"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> IsXAxisInvertedProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(IsXAxisInverted), false);
    /// <summary>
    /// Define <see cref="IsYAxisInverted"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> IsYAxisInvertedProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(IsYAxisInverted), false);
    /// <summary>
    /// Define <see cref="MaxSeriesValue"/> property.
    /// </summary>
    public static readonly ObservableProperty<DisplayableLogChartSeriesValue?> MaxSeriesValueProperty = ObservableProperty.Register<LogChartViewModel, DisplayableLogChartSeriesValue?>(nameof(MaxSeriesValue), null);
    /// <summary>
    /// Define <see cref="MaxVisibleSeriesValue"/> property.
    /// </summary>
    public static readonly ObservableProperty<DisplayableLogChartSeriesValue?> MaxVisibleSeriesValueProperty = ObservableProperty.Register<LogChartViewModel, DisplayableLogChartSeriesValue?>(nameof(MaxVisibleSeriesValue), null);
    /// <summary>
    /// Define <see cref="MinSeriesValue"/> property.
    /// </summary>
    public static readonly ObservableProperty<DisplayableLogChartSeriesValue?> MinSeriesValueProperty = ObservableProperty.Register<LogChartViewModel, DisplayableLogChartSeriesValue?>(nameof(MinSeriesValue), null);
    /// <summary>
    /// Define <see cref="MinVisibleSeriesValue"/> property.
    /// </summary>
    public static readonly ObservableProperty<DisplayableLogChartSeriesValue?> MinVisibleSeriesValueProperty = ObservableProperty.Register<LogChartViewModel, DisplayableLogChartSeriesValue?>(nameof(MinVisibleSeriesValue), null);
    /// <summary>
    /// Define <see cref="PanelSize"/> property.
    /// </summary>
    public static readonly ObservableProperty<double> PanelSizeProperty = ObservableProperty.Register<LogChartViewModel, double>(nameof(PanelSize), 300,
        coerce: (_, d) => Math.Max(MinPanelSize, d),
        validate: double.IsFinite);
    /// <summary>
    /// Define <see cref="XAxisName"/> property.
    /// </summary>
    public static readonly ObservableProperty<string?> XAxisNameProperty = ObservableProperty.Register<LogChartViewModel, string?>(nameof(XAxisName), null);
    /// <summary>
    /// Define <see cref="XAxisType"/> property.
    /// </summary>
    public static readonly ObservableProperty<LogChartXAxisType> XAxisTypeProperty = ObservableProperty.Register<LogChartViewModel, LogChartXAxisType>(nameof(XAxisType), LogChartXAxisType.None);
    /// <summary>
    /// Define <see cref="YAxisName"/> property.
    /// </summary>
    public static readonly ObservableProperty<string?> YAxisNameProperty = ObservableProperty.Register<LogChartViewModel, string?>(nameof(YAxisName), null);
    
    
    // Constants.
    const int ReportMinMaxSeriesValuesDelay = 500;
    const int ReportMinMaxStackedSeriesValuesDelay = 800;
    const string TempChartTypeStateKey = "LogChartViewModel.TempChartType";
    const string VisibleSeriesSourcesStateKey = $"LogChartViewModel.{nameof(VisibleSeriesSources)}";


    // Static fields.
    static readonly IValueConverter ChartValueGranularityConverter = new AppSuite.Converters.EnumConverter(IAppSuiteApplication.CurrentOrNull, typeof(LogChartValueGranularity));
    static readonly SettingKey<int> LatestPanelSizeKey = new("LogChartViewModel.LatestPanelSize", (int)(PanelSizeProperty.DefaultValue + 0.5));


    // Fields.
    DisplayableLogChartSeriesGenerator activeSeriesGenerator;
    readonly DisplayableLogChartSeriesGenerator allLogsSeriesGenerator;
    readonly Dictionary<DisplayableLogChartSeries, DisplayableLogChartSeriesGenerator> attachedSeries = new();
    readonly MutableObservableBoolean canSetChartType = new();
    DisplayableLogChartSeriesGenerator? emptySeriesGenerator;
    DisplayableLogChartSeriesGenerator? filteredLogsSeriesGenerator;
    readonly List<IDisposable> observerTokens = new();
    readonly DisplayableLogChartSeriesGenerator markedLogsSeriesGenerator;
    readonly ScheduledAction reportMaxSeriesValueAction;
    readonly ScheduledAction reportMinSeriesValueAction;
    readonly ScheduledAction reportSeriesAction;
    readonly Dictionary<DisplayableLogChartSeriesGenerator, PropertyChangedEventHandler> seriesPropertyChangedHandlers = new();
    readonly ObservableList<DisplayableLogChartSeriesSource> seriesSources = new();
    readonly Dictionary<DisplayableLogChartSeriesGenerator, NotifyCollectionChangedEventHandler> seriesValuesChangedHandlers = new();
    readonly ObservableList<DisplayableLogChartSeries> visibleSeries = new();
    readonly ObservableList<DisplayableLogChartSeriesSource> visibleSeriesSources = new();
    readonly ScheduledAction updateAxisNamesAction;


    /// <summary>
    /// Initialize new <see cref="LogChartViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogChartViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // create commands
        this.ResetVisibleSeriesSourcesCommand = new Command(this.ResetVisibleSeriesSources, this.GetValueAsObservable(IsChartDefinedProperty));
        this.SetChartTypeCommand = new Command<LogChartType>(this.SetChartType, this.canSetChartType);
        this.ToggleVisibleSeriesSourceCommand = new Command<DisplayableLogChartSeriesSource>(this.ToggleVisibleSeriesSource, this.GetValueAsObservable(IsChartDefinedProperty));
        
        // setup collections
        this.SeriesSources = ListExtensions.AsReadOnly(this.seriesSources);
        this.VisibleSeries = ListExtensions.AsReadOnly(this.visibleSeries);
        this.VisibleSeriesSources = ListExtensions.AsReadOnly(this.visibleSeriesSources);
        
        // create series generators
        var logComparer = new DisplayableLogComparer(this.CompareLogs, default);
        this.allLogsSeriesGenerator = new DisplayableLogChartSeriesGenerator(this.Application, this.AllLogs, logComparer).Also(this.AttachToSeriesGenerator);
        this.markedLogsSeriesGenerator = new DisplayableLogChartSeriesGenerator(this.Application, session.MarkedLogs, logComparer).Also(this.AttachToSeriesGenerator);
        this.activeSeriesGenerator = this.allLogsSeriesGenerator;
        
        // create actions
        this.reportMaxSeriesValueAction = new(this.ReportMaxSeriesValues);
        this.reportMinSeriesValueAction = new(this.ReportMinSeriesValues);
        this.reportSeriesAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            var session = this.Session;
            DisplayableLogChartSeriesGenerator seriesGenerator;
            if (session.IsShowingRawLogLinesTemporarily)
            {
                this.emptySeriesGenerator ??= new(this.Application, [], logComparer);
                seriesGenerator = this.emptySeriesGenerator;
            }
            else if (session.IsShowingMarkedLogsTemporarily)
                seriesGenerator = this.markedLogsSeriesGenerator;
            else if (session.LogFiltering.IsFilteringNeeded && !session.IsShowingAllLogsTemporarily)
                seriesGenerator = this.filteredLogsSeriesGenerator.AsNonNull();
            else
                seriesGenerator = this.allLogsSeriesGenerator;
            if (this.activeSeriesGenerator != seriesGenerator)
            {
                this.activeSeriesGenerator = seriesGenerator;
                this.OnPropertyChanged(nameof(HasChart));
                this.OnPropertyChanged(nameof(IsGeneratingSeries));
                this.OnPropertyChanged(nameof(IsMaxTotalSeriesValueCountReached));
                this.OnPropertyChanged(nameof(MaxSeriesValueCount));
                this.OnPropertyChanged(nameof(Series));
                this.RefreshVisibleSeries();
                this.reportMinSeriesValueAction.Execute();
                this.reportMaxSeriesValueAction.Execute();
            }
        });
        this.updateAxisNamesAction = new(() =>
        {
            var valueGranularity = this.LogProfile?.LogChartValueGranularity ?? LogChartValueGranularity.Default;
            switch (valueGranularity)
            {
                case LogChartValueGranularity.Default:
                case LogChartValueGranularity.Byte:
                case LogChartValueGranularity.Kilobytes:
                case LogChartValueGranularity.Megabytes:
                case LogChartValueGranularity.Gigabytes:
                case LogChartValueGranularity.Ten:
                    this.SetValue(YAxisNameProperty, null);
                    break;
                default:
                    this.SetValue(YAxisNameProperty, ChartValueGranularityConverter.Convert<string?>(valueGranularity));
                    break;
            }
        });
        
        // restore panel state
        this.SetValue(PanelSizeProperty, this.PersistentState.GetValueOrDefault(LatestPanelSizeKey));
        
        // attach to self properties
        var isInit = true;
        this.GetValueAsObservable(IsChartDefinedProperty).Subscribe(isDefined =>
        {
            if (isInit)
                return;
            if (!isDefined)
                this.ResetValue(IsPanelVisibleProperty);
            this.UpdateCanSetChartType();
        });
        this.GetValueAsObservable(PanelSizeProperty).Subscribe(size =>
        {
            if (!isInit)
                this.PersistentState.SetValue<int>(LatestPanelSizeKey, (int)(size + 0.5));
        });
        
        // attach to session
        this.observerTokens.Add(session.GetValueAsObservable(Session.IsProVersionActivatedProperty).Subscribe(_ =>
        {
            if (!isInit)
            {
                this.ApplyLogChartSeriesSources();
                this.UpdateCanSetChartType();
            }
        }));
        isInit = false;
    }
    
    
    // Set sources to series generators.
    void ApplyLogChartSeriesSources()
    {
        // get old sources
        var sourcesToDispose = this.allLogsSeriesGenerator.LogChartSeriesSources;
        
        // apply sources
        var sources = this.ConvertToDisplayableLogChartSources(this.LogProfile?.LogChartSeriesSources);
        if (this.Session.IsProVersionActivated)
        {
            this.allLogsSeriesGenerator.LogChartSeriesSources = sources;
            if (this.filteredLogsSeriesGenerator is not null)
                this.filteredLogsSeriesGenerator.LogChartSeriesSources = sources;
            this.markedLogsSeriesGenerator.LogChartSeriesSources = sources;
        }
        else
        {
            var emptySources = Array.Empty<DisplayableLogChartSeriesSource>();
            this.allLogsSeriesGenerator.LogChartSeriesSources = emptySources;
            if (this.filteredLogsSeriesGenerator is not null)
                this.filteredLogsSeriesGenerator.LogChartSeriesSources = emptySources;
            this.markedLogsSeriesGenerator.LogChartSeriesSources = emptySources;
        }
        this.SetValue(IsChartDefinedProperty, sources.IsNotEmpty() && this.LogProfile?.LogChartType != LogChartType.None);
        
        // dispose old sources
        foreach (var source in sourcesToDispose)
            source.Dispose();
    }


    // Apply type of log chart to generators.
    void ApplyLogChartType(LogChartType chartType)
    {
        this.allLogsSeriesGenerator.LogChartType = chartType;
        if (this.filteredLogsSeriesGenerator is not null)
            this.filteredLogsSeriesGenerator.LogChartType = chartType;
        this.markedLogsSeriesGenerator.LogChartType = chartType;
        this.UpdateAxisInversionState(chartType);
    }


    /// <summary>
    /// Check whether all source of series of log chart are visible or not.
    /// </summary>
    public bool AreAllSeriesSourcesVisible => this.GetValue(AreAllSeriesSourcesVisibleProperty);


    // Attach to given generator.
    void AttachToSeriesGenerator(DisplayableLogChartSeriesGenerator generator)
    {
        var seriesPropertyChangedHandler = new PropertyChangedEventHandler((_, e) =>
            this.OnSeriesPropertyChanged(generator, e));
        var seriesValuesChangedHandler = new NotifyCollectionChangedEventHandler((_, _) => 
            this.OnSeriesValuesChanged(generator));
        this.seriesPropertyChangedHandlers.Add(generator, seriesPropertyChangedHandler);
        this.seriesValuesChangedHandlers.Add(generator, seriesValuesChangedHandler);
        generator.PropertyChanged += this.OnSeriesGeneratorPropertyChanged;
        generator.Series.Let(series =>
        {
            ((INotifyCollectionChanged)series).CollectionChanged += (_, e) =>
                this.OnSeriesChanged(generator, e);
            foreach (var s in series)
            {
                if (this.attachedSeries.TryAdd(s, generator))
                {
                    s.PropertyChanged += seriesPropertyChangedHandler;
                    (s.Values as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += seriesValuesChangedHandler);
                }
            }
        });
    }


    /// <summary>
    /// Get type of chart.
    /// </summary>
    public LogChartType ChartType => this.GetValue(ChartTypeProperty);


    /// <summary>
    /// Get granularity of values of chart.
    /// </summary>
    public LogChartValueGranularity ChartValueGranularity => this.GetValue(ChartValueGranularityProperty);


    // Convert list of LogChartSeriesSource to list of DisplayableLogChartSeriesSource.
    DisplayableLogChartSeriesSource[] ConvertToDisplayableLogChartSources(IList<LogChartSeriesSource>? properties)
    {
        if (properties.IsNullOrEmpty())
            return [];
        var logChartProperties = new DisplayableLogChartSeriesSource[properties.Count];
        for (var i = properties.Count - 1; i >= 0; --i)
        {
            var source = properties[i];
            logChartProperties[i] = new(this.Application, source);
        }
        return logChartProperties;
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // collect sources to dispose
        var sourcesToDispose = this.allLogsSeriesGenerator.LogChartSeriesSources;
        
        // remove observers
        foreach (var token in this.observerTokens)
            token.Dispose();
        this.observerTokens.Clear();
        
        // detach from series
        foreach (var (generator, handler) in this.seriesPropertyChangedHandlers)
        {
            foreach (var series in generator.Series)
                series.PropertyChanged -= handler;
        }
        foreach (var (generator, handler) in this.seriesValuesChangedHandlers)
        {
            foreach (var series in generator.Series)
                (series.Values as INotifyCollectionChanged)?.Let(it => it.CollectionChanged -= handler);
        }
        this.seriesPropertyChangedHandlers.Clear();
        this.seriesValuesChangedHandlers.Clear();
        this.attachedSeries.Clear();
        
        // dispose generators
        this.allLogsSeriesGenerator.Dispose();
        this.filteredLogsSeriesGenerator?.Dispose();
        this.markedLogsSeriesGenerator.Dispose();
        
        // dispose sources
        foreach (var source in sourcesToDispose)
            source.Dispose();

            // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Get proper text of label on X axis.
    /// </summary>
    /// <param name="value">Value.</param>
    /// <returns>Text of label on X axis.</returns>
    public string GetXAxisLabel(double value) =>
        value.ToString(CultureInfo.InvariantCulture);


    /// <summary>
    /// Get proper text of label on Y axis.
    /// </summary>
    /// <param name="value">Value.</param>
    /// <returns>Text of label on Y axis.</returns>
    public string GetYAxisLabel(double value)
    {
        if (!this.GetValue(ChartTypeProperty).IsDirectNumberValueSeriesType())
            return value.ToString(CultureInfo.InvariantCulture);
        switch (this.LogProfile?.LogChartValueGranularity ?? LogChartValueGranularity.Default)
        {
            case LogChartValueGranularity.Byte:
                if (value < 1024)
                    return $"{value:F0} B";
                value /= 1024;
                goto case LogChartValueGranularity.Kilobytes;
            case LogChartValueGranularity.Kilobytes:
                if (value < 1024)
                    return $"{value:F2} KB";
                value /= 1024;
                goto case LogChartValueGranularity.Megabytes;
            case LogChartValueGranularity.Megabytes:
                if (value < 1024)
                    return $"{value:F2} MB";
                value /= 1024;
                goto case LogChartValueGranularity.Gigabytes;
            case LogChartValueGranularity.Gigabytes:
                return $"{value:F2} GB";
            case LogChartValueGranularity.Ten:
                return (value * 10).ToString("F0");
            default:
                return value.ToString(CultureInfo.InvariantCulture);
        }
    }


    /// <summary>
    /// Check whether log chart is available or not.
    /// </summary>
    public bool HasChart => this.activeSeriesGenerator.IsProcessingNeeded;


    /// <summary>
    /// Check whether log chart is defined in current log profile or not.
    /// </summary>
    public bool IsChartDefined => this.GetValue(IsChartDefinedProperty);


    /// <summary>
    /// Check whether series for chart is being generated or not.
    /// </summary>
    public bool IsGeneratingSeries => this.activeSeriesGenerator.IsProcessing;


    /// <summary>
    /// Get or set whether chart panel is visible or not.
    /// </summary>
    public bool IsPanelVisible
    {
        get => this.GetValue(IsPanelVisibleProperty);
        set => this.SetValue(IsPanelVisibleProperty, value);
    }


    /// <summary>
    /// Check whether total number of series values reaches the limitation or not.
    /// </summary>
    public bool IsMaxTotalSeriesValueCountReached => this.activeSeriesGenerator.IsMaxTotalSeriesValueCountReached;


    /// <summary>
    /// Check whether X-axis is needed to be inverted or not.
    /// </summary>
    public bool IsXAxisInverted => this.GetValue(IsXAxisInvertedProperty);
    
    
    /// <summary>
    /// Check whether Y-axis is needed to be inverted or not.
    /// </summary>
    public bool IsYAxisInverted => this.GetValue(IsYAxisInvertedProperty);


    /// <summary>
    /// Get known maximum value of all series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MaxSeriesValue => this.GetValue(MaxSeriesValueProperty);
    
    
    /// <summary>
    /// Get known maximum value of visible series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MaxVisibleSeriesValue => this.GetValue(MaxVisibleSeriesValueProperty);


    /// <summary>
    /// Get maximum number of values in all series.
    /// </summary>
    public int MaxSeriesValueCount => this.activeSeriesGenerator.MaxSeriesValueCount;


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize
                                       + this.allLogsSeriesGenerator.MemorySize
                                       + (this.filteredLogsSeriesGenerator?.MemorySize ?? 0L)
                                       + this.markedLogsSeriesGenerator.MemorySize;


    /// <summary>
    /// Get known minimum value of all series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MinSeriesValue => this.GetValue(MinSeriesValueProperty);
    
    
    /// <summary>
    /// Get known minimum value of visible series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MinVisibleSeriesValue => this.GetValue(MinVisibleSeriesValueProperty);


    /// <inheritdoc/>
    protected override void OnAllComponentsCreated()
    {
        // call base
        base.OnAllComponentsCreated();
        
        // create series generators
        var session = this.Session;
        this.filteredLogsSeriesGenerator = new DisplayableLogChartSeriesGenerator(this.Application, session.LogFiltering.FilteredLogs, new DisplayableLogComparer(this.CompareLogs, default)).Also(this.AttachToSeriesGenerator);
        
        // attach to session
        this.observerTokens.Add(session.GetValueAsObservable(Session.IsShowingAllLogsTemporarilyProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        this.observerTokens.Add(session.GetValueAsObservable(Session.IsShowingMarkedLogsTemporarilyProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        this.observerTokens.Add(session.GetValueAsObservable(Session.IsShowingRawLogLinesTemporarilyProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        
        // attach to components
        this.observerTokens.Add(session.LogFiltering.GetValueAsObservable(LogFilteringViewModel.IsFilteringNeededProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        
        // setup initial series
        this.reportSeriesAction.Execute();
    }


    /// <inheritdoc/>
    protected override void OnApplicationStringsUpdated()
    {
        base.OnApplicationStringsUpdated();
        this.updateAxisNamesAction.Schedule();
    }


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        // call base
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);

        // setup properties to generate series
        var logChartType = newLogProfile?.LogChartType ?? LogChartType.None;
        this.ApplyLogChartType(logChartType);
        this.SetValue(ChartTypeProperty, logChartType);
        this.SetValue(ChartValueGranularityProperty, newLogProfile?.LogChartValueGranularity ?? LogChartValueGranularity.Default);
        this.ApplyLogChartSeriesSources();
        if (this.GetValue(IsChartDefinedProperty) && this.Settings.GetValueOrDefault(SettingKeys.ShowLogChartPanelIfDefined))
            this.SetValue(IsPanelVisibleProperty, true);
        (newLogProfile?.SortDirection)?.Let(it =>
        {
            var logComparer = new DisplayableLogComparer(this.CompareLogs, it);
            this.allLogsSeriesGenerator.SourceLogComparer = logComparer;
            if (this.filteredLogsSeriesGenerator is not null)
                this.filteredLogsSeriesGenerator.SourceLogComparer = logComparer;
            this.markedLogsSeriesGenerator.SourceLogComparer = logComparer;
        });
        
        // setup properties of axes
        this.SetValue(XAxisTypeProperty, newLogProfile?.LogChartXAxisType ?? LogChartXAxisType.None);

        // report sources
        var seriesSources = newLogProfile?.LogChartSeriesSources.Let(it =>
        {
            var array = new DisplayableLogChartSeriesSource[it.Count];
            for (var i = it.Count - 1; i >= 0; --i)
                array[i] = new DisplayableLogChartSeriesSource(this.Application, it[i]);
            return ListExtensions.AsReadOnly(array);
        }) ?? Array.Empty<DisplayableLogChartSeriesSource>();
        this.visibleSeriesSources.Clear();
        this.seriesSources.Clear();
        this.seriesSources.AddAll(seriesSources);
        this.updateAxisNamesAction.Execute();
        this.UpdateCanSetChartType();

        // update visible series
        this.ResetVisibleSeriesSources();
    }


    /// <inheritdoc/>
    protected override void OnLogProfilePropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnLogProfilePropertyChanged(e);
        var profile = this.LogProfile;
        if (profile is null)
            return;
        switch (e.PropertyName)
        {
            case nameof(LogProfile.LogChartSeriesSources):
            {
                // show the panel
                var isPrevChartDefined = this.GetValue(IsChartDefinedProperty);
                this.ApplyLogChartSeriesSources();
                if (!isPrevChartDefined 
                    && this.GetValue(IsChartDefinedProperty) 
                    && this.Settings.GetValueOrDefault(SettingKeys.ShowLogChartPanelIfDefined))
                {
                    this.SetValue(IsPanelVisibleProperty, true);
                }
                
                // report sources
                var seriesSources = profile.LogChartSeriesSources.Let(it =>
                {
                    var array = new DisplayableLogChartSeriesSource[it.Count];
                    for (var i = it.Count - 1; i >= 0; --i)
                        array[i] = new DisplayableLogChartSeriesSource(this.Application, it[i]);
                    return ListExtensions.AsReadOnly(array);
                });
                this.visibleSeriesSources.Clear();
                this.seriesSources.Clear();
                this.seriesSources.AddAll(seriesSources);
                
                // update visible series
                this.ResetVisibleSeriesSources();
                break;
            }
            case nameof(LogProfile.LogChartType):
            {
                var isPrevChartDefined = this.GetValue(IsChartDefinedProperty);
                var logChartType = profile.LogChartType;
                this.ApplyLogChartType(logChartType);
                this.SetValue(ChartTypeProperty, logChartType);
                this.SetValue(IsChartDefinedProperty, profile.LogChartType != LogChartType.None && this.allLogsSeriesGenerator.LogChartSeriesSources.IsNotEmpty());
                if (!isPrevChartDefined 
                    && this.GetValue(IsChartDefinedProperty) 
                    && this.Settings.GetValueOrDefault(SettingKeys.ShowLogChartPanelIfDefined))
                {
                    this.SetValue(IsPanelVisibleProperty, true);
                }
                break;
            }
            case nameof(LogProfile.LogChartValueGranularity):
                this.SetValue(ChartValueGranularityProperty, profile.LogChartValueGranularity);
                this.updateAxisNamesAction.Schedule();
                break;
            case nameof(LogProfile.LogChartXAxisType):
                this.SetValue(XAxisTypeProperty, profile.LogChartXAxisType);
                break;
            case nameof(LogProfile.SortDirection):
                (this.LogProfile?.SortDirection)?.Let(it =>
                {
                    var logComparer = new DisplayableLogComparer(this.CompareLogs, it);
                    this.allLogsSeriesGenerator.SourceLogComparer = logComparer;
                    if (this.filteredLogsSeriesGenerator is not null)
                        this.filteredLogsSeriesGenerator.SourceLogComparer = logComparer;
                    this.markedLogsSeriesGenerator.SourceLogComparer = logComparer;
                    this.UpdateAxisInversionState(this.GetValue(ChartTypeProperty));
                });
                break;
        }
    }


    /// <inheritdoc/>
    protected override void OnRestoreState(JsonElement element)
    {
        base.OnRestoreState(element);
        if (element.TryGetProperty(TempChartTypeStateKey, out var jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.String
            && Enum.TryParse<LogChartType>(jsonProperty.GetString(), out var chartType))
        {
            this.ApplyLogChartType(chartType);
            this.SetValue(ChartTypeProperty, chartType);
        }
        if (element.TryGetProperty(VisibleSeriesSourcesStateKey, out jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.Array)
        {
            this.visibleSeriesSources.Clear();
            foreach (var jsonSource in jsonProperty.EnumerateArray())
            {
                if (DisplayableLogChartSeriesSource.TryLoad(this.Application, jsonSource, out var source)
                    && this.seriesSources.Contains(source))
                {
                    this.visibleSeriesSources.Add(source);
                }
            }
            
            this.Logger.LogDebug("{count} visible series source(s) restored", this.visibleSeriesSources.Count);
            
            this.SetValue(AreAllSeriesSourcesVisibleProperty, this.visibleSeriesSources.Count == this.seriesSources.Count);
            this.RefreshVisibleSeries();
        }
    }


    /// <inheritdoc/>
    protected override void OnSaveState(Utf8JsonWriter writer)
    {
        base.OnSaveState(writer);
        this.LogProfile?.Let(it =>
        {
            if (it.IsBuiltIn && it.LogChartType != this.GetValue(ChartTypeProperty))
                writer.WriteString(TempChartTypeStateKey, this.GetValue(ChartTypeProperty).ToString());
            if (this.visibleSeriesSources.IsNotEmpty())
            {
                writer.WritePropertyName(VisibleSeriesSourcesStateKey);
                writer.WriteStartArray();
                foreach (var source in this.visibleSeriesSources)
                    source.Save(writer);
                writer.WriteEndArray();
            }
        });
    }


    // Called when list of series changed.
    void OnSeriesChanged(DisplayableLogChartSeriesGenerator generator, NotifyCollectionChangedEventArgs e)
    {
        if (!this.seriesPropertyChangedHandlers.TryGetValue(generator, out var seriesPropertyChangedHandler)
            || !this.seriesValuesChangedHandlers.TryGetValue(generator, out var seriesValuesChangedHandler))
        {
            return;
        }
        var fromActiveGenerator = generator == this.activeSeriesGenerator;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var series in e.NewItems!.Cast<DisplayableLogChartSeries>())
                {
                    if (this.attachedSeries.TryAdd(series, generator))
                    {
                        series.PropertyChanged += seriesPropertyChangedHandler;
                        (series.Values as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += seriesValuesChangedHandler);
                    }
                    if (fromActiveGenerator)
                    {
                        var source = series.Source;
                        if (source is not null && !this.visibleSeriesSources.Contains(source))
                            continue;
                        this.visibleSeries.Add(series);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var series in e.OldItems!.Cast<DisplayableLogChartSeries>())
                {
                    if (this.attachedSeries.Remove(series))
                    {
                        series.PropertyChanged -= seriesPropertyChangedHandler;
                        (series.Values as INotifyCollectionChanged)?.Let(it => it.CollectionChanged -= seriesValuesChangedHandler);
                    }
                    if (fromActiveGenerator)
                        this.visibleSeries.Remove(series);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var (s, g) in this.attachedSeries.ToArray())
                {
                    if (g != generator)
                        continue;
                    if (!generator.Series.Contains(s))
                    {
                        this.attachedSeries.Remove(s);
                        s.PropertyChanged -= seriesPropertyChangedHandler;
                        (s.Values as INotifyCollectionChanged)?.Let(it => it.CollectionChanged -= seriesValuesChangedHandler);
                    }
                }
                foreach (var s in generator.Series)
                {
                    if (this.attachedSeries.TryAdd(s, generator))
                    {
                        s.PropertyChanged += seriesPropertyChangedHandler;
                        (s.Values as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += seriesValuesChangedHandler);
                    }
                }
                if (fromActiveGenerator)
                    this.RefreshVisibleSeries();
                break;
            default:
                throw new NotSupportedException($"Unsupported action of change of collection: {e.Action}.");
        }
        if (fromActiveGenerator)
        {
            if (this.activeSeriesGenerator.LogChartType.IsStackedSeriesType())
            {
                this.reportMinSeriesValueAction.Schedule(ReportMinMaxStackedSeriesValuesDelay);
                this.reportMaxSeriesValueAction.Schedule(ReportMinMaxStackedSeriesValuesDelay);
            }
            else
            {
                this.reportMinSeriesValueAction.Schedule(ReportMinMaxSeriesValuesDelay);
                this.reportMaxSeriesValueAction.Schedule(ReportMinMaxSeriesValuesDelay);
            }
        }
    }


    // Called when property of series generator changed.
    void OnSeriesGeneratorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DisplayableLogChartSeriesGenerator generator)
            return;
        switch (e.PropertyName)
        {
            case nameof(DisplayableLogChartSeriesGenerator.IsMaxTotalSeriesValueCountReached):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                    this.OnPropertyChanged(nameof(IsMaxTotalSeriesValueCountReached));
                break;
            case nameof(DisplayableLogChartSeriesGenerator.IsProcessing):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                    this.OnPropertyChanged(nameof(IsGeneratingSeries));
                break;
            case nameof(DisplayableLogChartSeriesGenerator.LogChartType):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                {
                    this.reportMinSeriesValueAction.Execute();
                    this.reportMaxSeriesValueAction.Execute();
                }
                break;
            case nameof(DisplayableLogChartSeriesGenerator.MaxSeriesValueCount):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                    this.OnPropertyChanged(nameof(MaxSeriesValueCount));
                break;
        }
    }


    // Called when property of one of series changed.
    void OnSeriesPropertyChanged(DisplayableLogChartSeriesGenerator generator, PropertyChangedEventArgs e)
    {
        if (this.activeSeriesGenerator != generator)
            return;
        switch (e.PropertyName)
        {
            case nameof(DisplayableLogChartSeries.MaxValue):
                if (!this.activeSeriesGenerator.LogChartType.IsStackedSeriesType())
                    this.reportMaxSeriesValueAction.Schedule(ReportMinMaxSeriesValuesDelay);
                break;
            case nameof(DisplayableLogChartSeries.MinValue):
                if (!this.activeSeriesGenerator.LogChartType.IsStackedSeriesType())
                    this.reportMinSeriesValueAction.Schedule(ReportMinMaxSeriesValuesDelay);
                break;
        }
    }


    // Called when values of series changed.
    void OnSeriesValuesChanged(DisplayableLogChartSeriesGenerator generator)
    {
        if (this.activeSeriesGenerator != generator && !generator.LogChartType.IsStackedSeriesType())
            return;
        this.reportMinSeriesValueAction.Schedule(ReportMinMaxStackedSeriesValuesDelay);
        this.reportMaxSeriesValueAction.Schedule(ReportMinMaxStackedSeriesValuesDelay);
    }


    // Refresh list of visible series.
    void RefreshVisibleSeries()
    {
        this.visibleSeries.Clear();
        foreach (var series in this.activeSeriesGenerator.Series)
        {
            var source = series.Source;
            if (source is not null && !this.visibleSeriesSources.Contains(source))
                continue;
            this.visibleSeries.Add(series);
        }
        this.reportMinSeriesValueAction.Execute();
        this.reportMaxSeriesValueAction.Execute();
    }


    // Set all series sources as visible.
    void ResetVisibleSeriesSources()
    {
        this.VerifyAccess();
        this.Logger.LogDebug("Reset visible series source(s): {count}", this.seriesSources.Count);
        if (this.visibleSeriesSources.Count == this.seriesSources.Count)
        {
            this.ResetValue(AreAllSeriesSourcesVisibleProperty);
            return;
        }
        this.visibleSeriesSources.Clear();
        this.visibleSeriesSources.AddAll(this.seriesSources);
        this.ResetValue(AreAllSeriesSourcesVisibleProperty);
        this.RefreshVisibleSeries();
    }
    
    
    /// <summary>
    /// Command to set all series sources as visible.
    /// </summary>
    public ICommand ResetVisibleSeriesSourcesCommand { get; }


    /// <summary>
    /// Get or set size of chart panel.
    /// </summary>
    public double PanelSize
    {
        get => this.GetValue(PanelSizeProperty);
        set => this.SetValue(PanelSizeProperty, value);
    }


    // Report maximum values of series.
    void ReportMaxSeriesValues()
    {
        if (this.IsDisposed)
            return;
        var series = this.activeSeriesGenerator.Series;
        var maxValue = default(DisplayableLogChartSeriesValue);
        var maxVisibleValue = default(DisplayableLogChartSeriesValue);
        var isVisibleSeries = new bool[series.Count].Also(it =>
        {
            for (var i = it.Length - 1; i >= 0; --i)
                it[i] = this.visibleSeries.Contains(series[i]);
        });
        if (this.activeSeriesGenerator.LogChartType.IsStackedSeriesType() && series.Count > 1)
        {
            var maxSeriesValueCount = 0;
            for (var i = series.Count - 1; i >= 0; --i)
                maxSeriesValueCount = Math.Max(maxSeriesValueCount, series[i].Values.Count);
            for (var i = maxSeriesValueCount - 1; i >= 0; --i)
            {
                var stackedDoubleValue = double.NaN;
                var visibleStackedDoubleValue = double.NaN;
                var areSameLog = true;
                var log = default(DisplayableLog);
                for (var j = series.Count - 1; j >= 0; --j)
                {
                    var values = series[j].Values;
                    if (values.Count < maxSeriesValueCount)
                        continue;
                    var doubleValue = values[i]?.Value ?? Double.NaN;
                    if (!double.IsFinite(doubleValue))
                        continue;
                    stackedDoubleValue = double.IsFinite(stackedDoubleValue)
                        ? stackedDoubleValue + doubleValue
                        : doubleValue;
                    if (isVisibleSeries[j])
                    {
                        visibleStackedDoubleValue = double.IsFinite(visibleStackedDoubleValue)
                            ? visibleStackedDoubleValue + doubleValue
                            : doubleValue;
                    }
                    if (areSameLog)
                    {
                        if (log is null)
                            log = values[i]!.Log;
                        else if (log != values[i]!.Log)
                        {
                            areSameLog = false;
                            log = null;
                        }
                    }
                }
                if (double.IsFinite(stackedDoubleValue))
                {
                    if (maxValue is null || maxValue.Value < stackedDoubleValue)
                        maxValue = new DisplayableLogChartSeriesValue(log, stackedDoubleValue);
                    if (maxVisibleValue is null || maxVisibleValue.Value < visibleStackedDoubleValue)
                        maxVisibleValue = new DisplayableLogChartSeriesValue(log, visibleStackedDoubleValue);
                }
            }
        }
        else
        {
            for (var i = series.Count - 1; i >= 0; --i)
            {
                var localMaxValue = series[i].MaxValue;
                if (localMaxValue is null)
                    continue;
                if (maxValue is null || maxValue.Value < localMaxValue.Value)
                    maxValue = localMaxValue;
                if (isVisibleSeries[i])
                {
                    if (maxVisibleValue is null || maxVisibleValue.Value < localMaxValue.Value)
                        maxVisibleValue = localMaxValue;
                }
            }
        }
        this.SetValue(MaxSeriesValueProperty, maxValue);
        this.SetValue(MaxVisibleSeriesValueProperty, maxVisibleValue);
    }
    
    
    // Report minimum values of series.
    void ReportMinSeriesValues()
    {
        if (this.IsDisposed)
            return;
        var series = this.activeSeriesGenerator.Series;
        var minValue = default(DisplayableLogChartSeriesValue);
        var minVisibleValue = default(DisplayableLogChartSeriesValue);
        var isVisibleSeries = new bool[series.Count].Also(it =>
        {
            for (var i = it.Length - 1; i >= 0; --i)
                it[i] = this.visibleSeries.Contains(series[i]);
        });
        if (this.activeSeriesGenerator.LogChartType.IsStackedSeriesType())
        {
            var maxSeriesValueCount = 0;
            for (var i = series.Count - 1; i >= 0; --i)
                maxSeriesValueCount = Math.Max(maxSeriesValueCount, series[i].Values.Count);
            for (var i = maxSeriesValueCount - 1; i >= 0; --i)
            {
                var stackedDoubleValue = double.NaN;
                var visibleStackedDoubleValue = double.NaN;
                var areSameLog = true;
                var log = default(DisplayableLog);
                for (var j = series.Count - 1; j >= 0; --j)
                {
                    var values = series[j].Values;
                    if (values.Count < maxSeriesValueCount)
                        continue;
                    var doubleValue = values[i]?.Value ?? Double.NaN;
                    if (!double.IsFinite(doubleValue))
                        continue;
                    stackedDoubleValue = double.IsFinite(stackedDoubleValue)
                        ? stackedDoubleValue + doubleValue
                        : doubleValue;
                    if (isVisibleSeries[j])
                    {
                        visibleStackedDoubleValue = double.IsFinite(visibleStackedDoubleValue)
                            ? visibleStackedDoubleValue + doubleValue
                            : doubleValue;
                    }
                    if (areSameLog)
                    {
                        if (log is null)
                            log = values[i]!.Log;
                        else if (log != values[i]!.Log)
                        {
                            areSameLog = false;
                            log = null;
                        }
                    }
                }
                if (double.IsFinite(stackedDoubleValue))
                {
                    if (minValue is null || minValue.Value > stackedDoubleValue)
                        minValue = new DisplayableLogChartSeriesValue(log, stackedDoubleValue);
                    if (minVisibleValue is null || minVisibleValue.Value > visibleStackedDoubleValue)
                        minVisibleValue = new DisplayableLogChartSeriesValue(log, visibleStackedDoubleValue);
                }
            }
        }
        else
        {
            for (var i = series.Count - 1; i >= 0; --i)
            {
                var localMinValue = series[i].MinValue;
                if (localMinValue is null)
                    continue;
                if (minValue is null || minValue.Value > localMinValue.Value)
                    minValue = localMinValue;
                if (isVisibleSeries[i])
                {
                    if (minVisibleValue is null || minVisibleValue.Value > localMinValue.Value)
                        minVisibleValue = localMinValue;
                }
            }
        }
        this.SetValue(MinSeriesValueProperty, minValue);
        this.SetValue(MinVisibleSeriesValueProperty, minVisibleValue);
    }


    /// <summary>
    /// Series of log chart.
    /// </summary>
    public IList<DisplayableLogChartSeries> Series => this.activeSeriesGenerator.Series;


    /// <summary>
    /// List of sources of series of log chart.
    /// </summary>
    public IList<DisplayableLogChartSeriesSource> SeriesSources { get; }


    // Set type of log chart.
    void SetChartType(LogChartType chartType)
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.LogProfile?.Let(it =>
        {
            if (!it.IsBuiltIn)
                it.LogChartType = chartType;
            else if (chartType != LogChartType.None)
            {
                this.ApplyLogChartType(chartType);
                this.SetValue(ChartTypeProperty, chartType);
            }
        });
    }
    
    
    /// <summary>
    /// Command set set type of log chart.
    /// </summary>
    /// <remarks>Type of parameter is <see cref="LogChartType"/>.</remarks>
    public ICommand SetChartTypeCommand { get; }


    // Toggle visibility of series.
    void ToggleVisibleSeriesSource(DisplayableLogChartSeriesSource? source)
    {
        // check state
        this.VerifyAccess();
        if (source is null)
            return;
        if (!this.seriesSources.Contains(source))
            return;
        
        // toggle visibility
        var visibleSeries = this.visibleSeries;
        if (this.visibleSeriesSources.Remove(source))
        {
            this.Logger.LogInformation("Remove 1 visible series source, total: {count}", this.visibleSeriesSources.Count);
            for (var i = visibleSeries.Count - 1; i >= 0; --i)
            {
                if (source.Equals(visibleSeries[i].Source))
                {
                    this.Logger.LogInformation("Remove visible series from position {index}", i);
                    visibleSeries.RemoveAt(i);
                    break;
                }
            }
            this.SetValue(AreAllSeriesSourcesVisibleProperty, false);
        }
        else if (!this.visibleSeriesSources.Contains(source))
        {
            this.visibleSeriesSources.Add(source);
            this.Logger.LogInformation("Add 1 visible series source, total: {count}", this.visibleSeriesSources.Count);
            foreach (var series in this.activeSeriesGenerator.Series)
            {
                if (source.Equals(series.Source))
                {
                    this.Logger.LogInformation("Add visible series to position {index}", this.visibleSeries.Count);
                    this.visibleSeries.Add(series);
                    break;
                }
            }
            if (this.seriesSources.Count == this.visibleSeriesSources.Count)
                this.ResetValue(AreAllSeriesSourcesVisibleProperty);
        }
        else
            return;
        
        // report min/max values
        this.reportMinSeriesValueAction.Execute();
        this.reportMaxSeriesValueAction.Execute();
    }
    
    
    /// <summary>
    /// Command to toggle visibility of series.
    /// </summary>
    /// <remarks>The type of parameter is <see cref="DisplayableLogChartSeriesSource"/>.</remarks>
    public ICommand ToggleVisibleSeriesSourceCommand { get; }
    
    
    // Update inversion state of axes.
    void UpdateAxisInversionState(LogChartType chartType)
    {
        if (this.LogProfile is not { } logProfile || logProfile.SortDirection == SortDirection.Ascending)
        {
            this.ResetValue(IsXAxisInvertedProperty);
            return;
        }
        this.SetValue(IsXAxisInvertedProperty, chartType switch
        {
            LogChartType.None
                or LogChartType.ValueStatisticBars => false,
            _ => true,
        });
    }
    
    
    // Update whether setting type of log chart is available or not.
    void UpdateCanSetChartType()
    {
        this.canSetChartType.Update(this.IsChartDefined && this.Session.IsProVersionActivated);
    }
    
    
    /// <summary>
    /// Visible series of log chart.
    /// </summary>
    public IList<DisplayableLogChartSeries> VisibleSeries { get; }
    
    
    /// <summary>
    /// Visible source of series of log chart.
    /// </summary>
    public IList<DisplayableLogChartSeriesSource> VisibleSeriesSources { get; }
    
    
    /// <summary>
    /// Get name of X-axis.
    /// </summary>
    public string? XAxisName => this.GetValue(XAxisNameProperty);


    /// <summary>
    /// Get type of X axis of log chart.
    /// </summary>
    public LogChartXAxisType XAxisType => this.GetValue(XAxisTypeProperty);


    /// <summary>
    /// Get name of Y-axis.
    /// </summary>
    public string? YAxisName => this.GetValue(YAxisNameProperty);
}