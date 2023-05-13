using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log chart.
/// </summary>
class LogChartViewModel : SessionComponent
{
    /// <summary>
    /// Minimum size of chart panel.
    /// </summary>
    public const double MinPanelSize = 130;
    
    
    /// <summary>
    /// Define <see cref="ChartType"/> property.
    /// </summary>
    public static readonly ObservableProperty<LogChartType> ChartTypeProperty = ObservableProperty.Register<LogChartViewModel, LogChartType>(nameof(ChartType), LogChartType.None);
    /// <summary>
    /// Define <see cref="IsChartDefined"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> IsChartDefinedProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(IsChartDefined), false);
    /// <summary>
    /// Define <see cref="IsPanelVisible"/> property.
    /// </summary>
    public static readonly ObservableProperty<bool> IsPanelVisibleProperty = ObservableProperty.Register<LogChartViewModel, bool>(nameof(IsPanelVisible), false);
    /// <summary>
    /// Define <see cref="PanelSize"/> property.
    /// </summary>
    public static readonly ObservableProperty<double> PanelSizeProperty = ObservableProperty.Register<LogChartViewModel, double>(nameof(PanelSize), 300,
        coerce: (_, d) => Math.Max(MinPanelSize, d),
        validate: double.IsFinite);


    // Static fields.
    static readonly SettingKey<int> LatestPanelSizeKey = new("LogChartViewModel.LatestPanelSize", (int)(PanelSizeProperty.DefaultValue + 0.5));


    // Fields.
    DisplayableLogChartSeriesGenerator activeSeriesGenerator;
    readonly DisplayableLogChartSeriesGenerator allLogsSeriesGenerator;
    readonly MutableObservableBoolean canSetChartType = new();
    DisplayableLogChartSeriesGenerator? filteredLogsSeriesGenerator;
    readonly List<IDisposable> observerTokens = new();
    readonly DisplayableLogChartSeriesGenerator markedLogsSeriesGenerator;
    readonly ScheduledAction reportSeriesAction;


    /// <summary>
    /// Initialize new <see cref="LogChartViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogChartViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // create commands
        this.SetChartTypeCommand = new Command<LogChartType>(this.SetChartType, this.canSetChartType);
        
        // create series generators
        this.allLogsSeriesGenerator = new DisplayableLogChartSeriesGenerator(this.Application, this.AllLogs, this.CompareLogs).Also(it =>
        {
            it.PropertyChanged += this.OnSeriesGeneratorPropertyChanged;
        });
        this.markedLogsSeriesGenerator = new DisplayableLogChartSeriesGenerator(this.Application, session.MarkedLogs, this.CompareLogs).Also(it =>
        {
            it.PropertyChanged += this.OnSeriesGeneratorPropertyChanged;
        });
        this.activeSeriesGenerator = this.allLogsSeriesGenerator;
        
        // create actions
        this.reportSeriesAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            var session = this.Session;
            DisplayableLogChartSeriesGenerator seriesGenerator;
            if (session.IsShowingMarkedLogsTemporarily)
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
                this.OnPropertyChanged(nameof(MaxSeriesValue));
                this.OnPropertyChanged(nameof(MaxSeriesValueCount));
                this.OnPropertyChanged(nameof(MinSeriesValue));
                this.OnPropertyChanged(nameof(Series));
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


    /// <summary>
    /// Get type of chart.
    /// </summary>
    public LogChartType ChartType => this.GetValue(ChartTypeProperty);


    // Convert list of LogChartSeriesSource to list of DisplayableLogChartSeriesSource.
    DisplayableLogChartSeriesSource[] ConvertToDisplayableLogChartSources(IList<LogChartSeriesSource>? properties)
    {
        if (properties.IsNullOrEmpty())
            return Array.Empty<DisplayableLogChartSeriesSource>();
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
    /// Get known maximum value of all series.
    /// </summary>
    public DisplayableLogChartSeriesValue? MaxSeriesValue => this.activeSeriesGenerator.MaxSeriesValue;


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
    public DisplayableLogChartSeriesValue? MinSeriesValue => this.activeSeriesGenerator.MinSeriesValue;


    /// <inheritdoc/>
    protected override void OnAllComponentsCreated()
    {
        // call base
        base.OnAllComponentsCreated();
        
        // create series generators
        var session = this.Session;
        this.filteredLogsSeriesGenerator = new DisplayableLogChartSeriesGenerator(this.Application, session.LogFiltering.FilteredLogs, this.CompareLogs).Also(it =>
        {
            it.PropertyChanged += this.OnSeriesGeneratorPropertyChanged;
        });
        
        // attach to session
        this.observerTokens.Add(session.GetValueAsObservable(Session.IsShowingAllLogsTemporarilyProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        this.observerTokens.Add(session.GetValueAsObservable(Session.IsShowingMarkedLogsTemporarilyProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        
        // attach to components
        this.observerTokens.Add(session.LogFiltering.GetValueAsObservable(LogFilteringViewModel.IsFilteringNeededProperty).Subscribe(_ => this.reportSeriesAction.Schedule()));
        
        // setup initial series
        this.reportSeriesAction.Execute();
    }


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        // call base
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);
        
        // setup properties to generate series
        var logChartType = newLogProfile?.LogChartType ?? LogChartType.None;
        this.allLogsSeriesGenerator.LogChartType = logChartType;
        if (this.filteredLogsSeriesGenerator is not null)
            this.filteredLogsSeriesGenerator.LogChartType = logChartType;
        this.markedLogsSeriesGenerator.LogChartType = logChartType;
        this.SetValue(ChartTypeProperty, logChartType);
        this.ApplyLogChartSeriesSources();
        if (this.GetValue(IsChartDefinedProperty) && this.Settings.GetValueOrDefault(SettingKeys.ShowLogChartPanelIfDefined))
            this.SetValue(IsPanelVisibleProperty, true);
        this.UpdateCanSetChartType();
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
                var isPrevChartDefined = this.GetValue(IsChartDefinedProperty);
                this.ApplyLogChartSeriesSources();
                if (!isPrevChartDefined 
                    && this.GetValue(IsChartDefinedProperty) 
                    && this.Settings.GetValueOrDefault(SettingKeys.ShowLogChartPanelIfDefined))
                {
                    this.SetValue(IsPanelVisibleProperty, true);
                }
                break;
            }
            case nameof(LogProfile.LogChartType):
            {
                var isPrevChartDefined = this.GetValue(IsChartDefinedProperty);
                var logChartType = profile.LogChartType;
                this.allLogsSeriesGenerator.LogChartType = logChartType;
                if (this.filteredLogsSeriesGenerator is not null)
                    this.filteredLogsSeriesGenerator.LogChartType = logChartType;
                this.markedLogsSeriesGenerator.LogChartType = logChartType;
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
            case nameof(DisplayableLogChartSeriesGenerator.MaxSeriesValue):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                    this.OnPropertyChanged(nameof(MaxSeriesValue));
                break;
            case nameof(DisplayableLogChartSeriesGenerator.MaxSeriesValueCount):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                    this.OnPropertyChanged(nameof(MaxSeriesValueCount));
                break;
            case nameof(DisplayableLogChartSeriesGenerator.MinSeriesValue):
                if (this.activeSeriesGenerator == generator && !this.IsDisposed)
                    this.OnPropertyChanged(nameof(MinSeriesValue));
                break;
        }
    }


    /// <summary>
    /// Get or set size of chart panel.
    /// </summary>
    public double PanelSize
    {
        get => this.GetValue(PanelSizeProperty);
        set => this.SetValue(PanelSizeProperty, value);
    }


    /// <summary>
    /// Series of log chart.
    /// </summary>
    public IList<DisplayableLogChartSeries> Series => this.activeSeriesGenerator.Series;


    // Set type of log chart.
    void SetChartType(LogChartType chartType)
    {
        this.VerifyAccess();
        this.VerifyDisposed();
        this.LogProfile?.Let(it => it.LogChartType = chartType);
    }
    
    
    /// <summary>
    /// Command set set type of log chart.
    /// </summary>
    /// <remarks>Type of parameter is <see cref="LogChartType"/>.</remarks>
    public ICommand SetChartTypeCommand { get; }
    
    
    // Update whether setting type of log chart is available or not.
    void UpdateCanSetChartType()
    {
        this.canSetChartType.Update(this.IsChartDefined && this.Session.IsProVersionActivated && this.LogProfile?.IsBuiltIn != true);
    }
}