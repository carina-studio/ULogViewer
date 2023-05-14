using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls;

partial class SessionView
{
    /// <summary>
    /// Define <see cref="IsLogChartHorizontallyZoomed"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsLogChartHorizontallyZoomedProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsLogChartHorizontallyZoomed), false);
    /// <summary>
    /// Define <see cref="LogChartLegendBackgroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartLegendBackgroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartLegendBackgroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartLegendForegroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartLegendForegroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartLegendForegroundPaint), new SolidColorPaint());
    /// <summary>
    /// <see cref="IValueConverter"/> to convert <see cref="LogChartType"/> to readable name.
    /// </summary>
    public static readonly IValueConverter LogChartTypeNameConverter = new AppSuite.Converters.EnumConverter(App.CurrentOrNull, typeof(LogChartType));
    /// <summary>
    /// Define <see cref="LogChartToolTipBackgroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartToolTipBackgroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartToolTipBackgroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartToolTipForegroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartToolTipForegroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartToolTipForegroundPaint), new SolidColorPaint());
    
    
    // Constants.
    const long DurationToDropClickEvent = 500;
    const double PointerDistanceToDropClickEvent = 5;
    const int LogChartXAxisMinValueCount = 10;
    const double LogChartXAxisMinMaxReservedRatio = 0.01;
    const double LogChartYAxisMinMaxReservedRatio = 0.05;
    
    
    // Static fields.
    static readonly SKColor[] LogChartSeriesColorsDark =
    {
        SKColor.FromHsl(0, 100, 60), // Red
        SKColor.FromHsl(30, 100, 60), // Orange
        SKColor.FromHsl(53, 100, 60), // Yellow
        SKColor.FromHsl(53, 100, 30), // Dark Yellow
        SKColor.FromHsl(95, 100, 60), // Green
        SKColor.FromHsl(95, 100, 30), // Dark Green
        SKColor.FromHsl(185, 100, 60), // Blue
        SKColor.FromHsl(185, 100, 30), // Dark Blue
        SKColor.FromHsl(220, 100, 60), // Navy
        SKColor.FromHsl(260, 100, 60), // Purple
        SKColor.FromHsl(315, 100, 60), // Magenta
    };
    static readonly SKColor[] LogChartSeriesColorsLight =
    {
        SKColor.FromHsl(0, 100, 35), // Red
        SKColor.FromHsl(30, 100, 35), // Orange
        SKColor.FromHsl(53, 100, 35), // Yellow
        SKColor.FromHsl(53, 100, 20), // Dark Yellow
        SKColor.FromHsl(95, 100, 35), // Green
        SKColor.FromHsl(95, 100, 20), // Dark Green
        SKColor.FromHsl(185, 100, 35), // Blue
        SKColor.FromHsl(200, 100, 35), // Dark Blue
        SKColor.FromHsl(220, 100, 40), // Navy
        SKColor.FromHsl(260, 100, 50), // Purple
        SKColor.FromHsl(315, 100, 40), // Magenta
    };
    static readonly SettingKey<bool> PromptWhenMaxTotalLogSeriesValueCountReachedKey = new("SessionView.PromptWhenMaxTotalLogSeriesValueCountReached", true);


    // Fields.
    bool areLogChartAxesReady;
    INotifyCollectionChanged? attachedRawLogChartSeries;
    bool isLogChartDoubleTapped;
    bool isSyncingLogChartPanelSize;
    readonly CartesianChart logChart;
    readonly RowDefinition logChartGridRow;
    ChartPoint[] logChartPointerDownData = Array.Empty<ChartPoint>();
    Point? logChartPointerDownPosition;
    readonly Stopwatch logChartPointerDownWatch = new();
    readonly ObservableList<ISeries> logChartSeries = new();
    readonly List<SKColor> logChartSeriesColorPool = new();
    readonly Dictionary<string, SKColor> logChartSeriesColors = new();
    readonly ToggleButton logChartTypeButton;
    readonly ContextMenu logChartTypeMenu;
    readonly Axis logChartXAxis = new()
    {
        CrosshairSnapEnabled = true,
    };
    readonly Axis logChartYAxis = new()
    {
        CrosshairSnapEnabled = true,
    };
    IPaint<SkiaSharpDrawingContext>? logChartYAxisCrosshairPaint;
    readonly ScheduledAction updateLogChartXAxisLimitAction;
    readonly ScheduledAction updateLogChartYAxisLimitAction;
    
    
    // Attach to given raw series of log chart.
    void AttachToRawLogChartSeries(IList<DisplayableLogChartSeries> rawSeries)
    {
        this.DetachFromRawLogChartSeries();
        this.attachedRawLogChartSeries = rawSeries as INotifyCollectionChanged;
        if (this.attachedRawLogChartSeries is not null)
            this.attachedRawLogChartSeries.CollectionChanged += this.OnRawLogChartSeriesChanged;
        this.UpdateLogChartSeries();
    }
    
    
    // Create single series of log chart.
    ISeries CreateLogChartSeries(LogChartViewModel viewModel, DisplayableLogChartSeries series)
    {
        // select color
        var color = SelectLogChartSeriesColor(series.Source?.PropertyName);
        
        // create series
        return viewModel.ChartType switch
        {
            LogChartType.ValueStatisticBars 
                or LogChartType.ValueBars => new ColumnSeries<DisplayableLogChartSeriesValue>
            {
                Fill = new SolidColorPaint(color),
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.Source?.PropertyDisplayName,
                Padding = 1,
                Rx = 0,
                Ry = 0,
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.Source?.PropertyDisplayName}: {point.PrimaryValue}";
                },
                Values = series.Values,
            },
            LogChartType.ValueStackedAreas
                or LogChartType.ValueStackedAreasWithDataPoints => new StackedAreaSeries<DisplayableLogChartSeriesValue>
            {
                Fill = new SolidColorPaint(color.WithAlpha((byte)(color.Alpha * 0.8))),
                GeometryFill = viewModel.ChartType == LogChartType.ValueStackedAreasWithDataPoints ? new SolidColorPaint(color) { IsAntialias = true } : null,
                GeometrySize = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Point.Size", 6.0),
                GeometryStroke = null,
                LineSmoothness = 0,
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.Source?.PropertyDisplayName,
                Stroke = new SolidColorPaint(color, (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Width", 2.0))
                {
                    IsAntialias = true,
                },
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.Source?.PropertyDisplayName}: {point.PrimaryValue}";
                },
                Values = series.Values,
            },
            LogChartType.ValueStackedBars => new StackedColumnSeries<DisplayableLogChartSeriesValue>
            {
                Fill = new SolidColorPaint(color),
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.Source?.PropertyDisplayName,
                Padding = 1,
                Rx = 0,
                Ry = 0,
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.Source?.PropertyDisplayName}: {point.PrimaryValue}";
                },
                Values = series.Values,
            },
            _ => new LineSeries<DisplayableLogChartSeriesValue>
            {
                Fill = viewModel.ChartType switch
                {
                    LogChartType.ValueAreas
                        or LogChartType.ValueAreasWithDataPoints => new SolidColorPaint(color.WithAlpha((byte)(color.Alpha * 0.5))),
                    _ => null,
                },
                GeometryFill = viewModel.ChartType switch
                {
                    LogChartType.ValueAreasWithDataPoints
                        or LogChartType.ValueCurvesWithDataPoints
                        or LogChartType.ValueLinesWithDataPoints => new SolidColorPaint(color) { IsAntialias = true },
                    _ => null,
                },
                GeometrySize = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Point.Size", 6.0),
                GeometryStroke = null,
                LineSmoothness = viewModel.ChartType switch
                {
                    LogChartType.ValueCurves
                        or LogChartType.ValueCurvesWithDataPoints => 1,
                    _ => 0,
                },
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.Source?.PropertyDisplayName,
                Stroke = new SolidColorPaint(color, (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Width", 2.0))
                {
                    IsAntialias = true,
                },
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.Source?.PropertyDisplayName}: {point.PrimaryValue}";
                },
                Values = series.Values,
            },
        };
    }
    
    
    // Detach from current raw series of log chart.
    void DetachFromRawLogChartSeries()
    {
        if (this.attachedRawLogChartSeries is not null)
            this.attachedRawLogChartSeries.CollectionChanged -= this.OnRawLogChartSeriesChanged;
        this.attachedRawLogChartSeries = null;
        this.logChartSeries.Clear();
    }


    // Invalidate colors of log chart series.
    void InvalidateLogChartSeriesColors()
    {
        this.logChartSeriesColorPool.Clear();
        this.logChartSeriesColors.Clear();
    }


    /// <summary>
    /// Check whether log chart has been zoomed horizontally.
    /// </summary>
    public bool IsLogChartHorizontallyZoomed => this.GetValue(IsLogChartHorizontallyZoomedProperty);


    /// <summary>
    /// Get background paint for legend of log chart.
    /// </summary>
    public IPaint<SkiaSharpDrawingContext> LogChartLegendBackgroundPaint => this.GetValue(LogChartLegendBackgroundPaintProperty);
    
    
    /// <summary>
    /// Get foreground paint for legend of log chart.
    /// </summary>
    public IPaint<SkiaSharpDrawingContext> LogChartLegendForegroundPaint => this.GetValue(LogChartLegendForegroundPaintProperty);


    /// <summary>
    /// Get background paint for tool tip of log chart.
    /// </summary>
    public IPaint<SkiaSharpDrawingContext> LogChartToolTipBackgroundPaint => this.GetValue(LogChartToolTipBackgroundPaintProperty);


    /// <summary>
    /// Get foreground paint for tool tip of log chart.
    /// </summary>
    public IPaint<SkiaSharpDrawingContext> LogChartToolTipForegroundPaint => this.GetValue(LogChartToolTipForegroundPaintProperty);


    /// <summary>
    /// Series of log chart.
    /// </summary>
    public IList<ISeries> LogChartSeries => this.logChartSeries;
    
    
    /// <summary>
    /// X axes of log chart.
    /// </summary>
    public IList<Axis> LogChartXAxes { get; }
    
    
    /// <summary>
    /// X axes of log chart.
    /// </summary>
    public IList<Axis> LogChartYAxes { get; }
    
    
    // Called when property of axis of log chart changed.
    void OnLogChartAxisPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == this.logChartXAxis)
        {
            switch (e.PropertyName)
            {
                case nameof(Axis.MaxLimit):
                case nameof(Axis.MinLimit):
                    this.updateLogChartXAxisLimitAction.Schedule();
                    break;
            }
        }
    }


    // Called when pointer entered or leave the chart.
    void OnLogChartPointerOverChanged(bool isPointerOver)
    {
        var paint = isPointerOver && this.DataContext is Session session
            ? session.LogChart.ChartType switch
            {
                LogChartType.ValueStackedAreas
                    or LogChartType.ValueStackedAreasWithDataPoints
                    or LogChartType.ValueStackedBars => null,
                _ => this.logChartYAxisCrosshairPaint,
            }
            : null;
        this.logChartXAxis.CrosshairPaint = paint;
        this.logChartYAxis.CrosshairPaint = paint;
    }
    
    
    // Called when pointer down on data points in log chart.
    void OnLogChartDataPointerDown(IEnumerable<ChartPoint> points)
    {
        if (this.logChartPointerDownPosition.HasValue)
            this.logChartPointerDownData = points.ToArray();
    }


    // Called when clicking on data points in log chart.
    void OnLogChartDataClick(ChartPoint[] points)
    {
        var log = default(DisplayableLog);
        foreach (var point in points)
        {
            if (point.Context.Series.Values is not IList<DisplayableLogChartSeriesValue> values)
                continue;
            var index = (int)(point.SecondaryValue + 0.5);
            if (index < 0 || index >= values.Count)
                continue;
            var candidateLog = values[index].Log;
            if (candidateLog is null)
                continue;
            if (log is null)
                log = candidateLog;
            else if (log != candidateLog)
            {
                log = null;
                break;
            }
        }
        if (log is not null)
        {
            this.logListBox.SelectedItems?.Clear();
            this.logListBox.SelectedItem = log;
            this.ScrollToLog(log, true);
        }
    }
    
    
    // Called when pointer moved on log chart.
    void OnLogChartPointerMoved(object? sender, PointerEventArgs e)
    {
        if (this.logChartPointerDownPosition.HasValue && this.logChartPointerDownData.Length > 0)
        {
            var downPosition = this.logChartPointerDownPosition.Value;
            var position = e.GetPosition(this.logChart);
            var diffX = (position.X - downPosition.X);
            var diffY = (position.Y - downPosition.Y);
            var distance = Math.Sqrt(diffX * diffX + diffY * diffY);
            if (distance >= PointerDistanceToDropClickEvent)
            {
                this.logChartPointerDownData = Array.Empty<ChartPoint>();
                this.logChartPointerDownPosition = null;
                this.logChartPointerDownWatch.Reset();
            }
        }
    }


    // Called when pointer pressed on log chart.
    void OnLogChartPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this.logChart);
        if (point.Properties.IsLeftButtonPressed)
        {
            this.logChartPointerDownPosition = point.Position;
            this.logChartPointerDownWatch.Restart();
        }
    }
    
    
    // Called when pointer released on log chart.
    void OnLogChartPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            // double click
            if (this.isLogChartDoubleTapped)
            {
                this.isLogChartDoubleTapped = false;
                this.logChartPointerDownData = Array.Empty<ChartPoint>();
                this.logChartPointerDownPosition = null;
                this.logChartPointerDownWatch.Reset();
                this.SynchronizationContext.PostDelayed(this.ResetLogChartZoom, 100);
                return;
            }
            
            // single click
            if (this.logChartPointerDownData.Length > 0)
            {
                var data = this.logChartPointerDownData;
                this.logChartPointerDownData = Array.Empty<ChartPoint>();
                if (this.logChartPointerDownPosition.HasValue)
                {
                    var downPosition = this.logChartPointerDownPosition.Value;
                    var upPosition = e.GetPosition(this.logChart);
                    var diffX = (upPosition.X - downPosition.X);
                    var diffY = (upPosition.Y - downPosition.Y);
                    var distance = Math.Sqrt(diffX * diffX + diffY * diffY);
                    this.logChartPointerDownPosition = null;
                    if (distance < PointerDistanceToDropClickEvent && this.logChartPointerDownWatch.ElapsedMilliseconds < DurationToDropClickEvent)
                        this.OnLogChartDataClick(data);
                }
                this.logChartPointerDownWatch.Reset();
            }
        }
    }


    // Called when size of log chart changed.
    void OnLogChartSizeChanged(SizeChangedEventArgs e)
    {
        if (this.isSyncingLogChartPanelSize
            || this.DataContext is not Session session
            || !session.LogChart.IsPanelVisible)
        {
            return;
        }
        this.isSyncingLogChartPanelSize = true;
        session.LogChart.PanelSize = e.NewSize.Height;
        this.isSyncingLogChartPanelSize = false;
    }
    
    
    // Called when property of view-model of log chart changed.
    void OnLogChartViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LogChartViewModel viewModel)
            return;
        switch (e.PropertyName)
        {
            case nameof(LogChartViewModel.ChartType):
            case nameof(LogChartViewModel.IsChartDefined):
                this.UpdateLogChartSeries();
                break;
            case nameof(LogChartViewModel.IsMaxTotalSeriesValueCountReached):
                this.PromptForMaxLogChartSeriesValueCountReached();
                break;
            case nameof(LogChartViewModel.IsPanelVisible):
                this.UpdateLogChartPanelVisibility();
                break;
            case nameof(LogChartViewModel.MaxSeriesValue):
            case nameof(LogChartViewModel.MinSeriesValue):
                this.updateLogChartYAxisLimitAction.Schedule();
                break;
            case nameof(LogChartViewModel.MaxSeriesValueCount):
                this.logChart.ZoomMode = viewModel.MaxSeriesValueCount > LogChartXAxisMinValueCount 
                    ? ZoomAndPanMode.X 
                    : ZoomAndPanMode.None;
                this.updateLogChartXAxisLimitAction.Schedule();
                break;
            case nameof(LogChartViewModel.PanelSize):
                if (!this.isSyncingLogChartPanelSize && viewModel.IsPanelVisible)
                {
                    this.isSyncingLogChartPanelSize = true;
                    this.logChartGridRow.Height = new(viewModel.PanelSize);
                    this.isSyncingLogChartPanelSize = false;
                }
                break;
            case nameof(LogChartViewModel.Series):
                this.AttachToRawLogChartSeries(viewModel.Series);
                break;
        }
    }


    // Called when collection of series changed by view-model.
    void OnRawLogChartSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.DataContext is not Session session)
            return;
        var viewModel = session.LogChart;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<DisplayableLogChartSeries>().Let(series =>
                {
                    var startIndex = e.NewStartingIndex;
                    for (int i = 0, count = series.Count; i < count; ++i)
                        this.logChartSeries.Insert(startIndex + i, this.CreateLogChartSeries(viewModel, series[i]));
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogChartSeries>().Let(series =>
                    this.logChartSeries.RemoveRange(e.OldStartingIndex, series.Count));
                break;
            case NotifyCollectionChangedAction.Reset:
                this.UpdateLogChartSeries();
                break;
            default:
                throw new NotSupportedException();
        }
    }


    // Show message dialog to notify user that the total number of values reaches the limitation.
    async void PromptForMaxLogChartSeriesValueCountReached()
    {
        if (!this.PersistentState.GetValueOrDefault(PromptWhenMaxTotalLogSeriesValueCountReachedKey))
            return;
        if (this.attachedWindow is null)
            return;
        var dialog = new MessageDialog
        {
            DoNotAskOrShowAgain = true,
            Icon = MessageDialogIcon.Warning,
            Message = this.Application.GetObservableString("SessionView.MaxTotalLogChartSeriesValueCountReached"),
        };
        await dialog.ShowDialog(this.attachedWindow);
        if (dialog.DoNotAskOrShowAgain == true)
            this.PersistentState.SetValue<bool>(PromptWhenMaxTotalLogSeriesValueCountReachedKey, false);
    }


    /// <summary>
    /// Reset zoom on log chart.
    /// </summary>
    public void ResetLogChartZoom()
    {
        this.logChartXAxis.Let(it =>
        {
            it.MinLimit = null;
            it.MaxLimit = null;
        });
        this.updateLogChartXAxisLimitAction.Execute();
    }
    
    
    /// <summary>
    /// Horizontally scroll to end of log chart.
    /// </summary>
    public void ScrollToEndOfLogChart()
    {
        var minLimit = this.logChartXAxis.MinLimit ?? double.NaN;
        var maxLimit = this.logChartXAxis.MaxLimit ?? double.NaN;
        if (!double.IsFinite(minLimit) && !double.IsFinite(maxLimit))
            return;
        if (this.DataContext is not Session session)
            return;
        var maxSeriesValueCount = 0;
        foreach (var series in session.LogChart.Series)
            maxSeriesValueCount = Math.Max(maxSeriesValueCount, series.Values.Count);
        var length = (maxLimit - minLimit);
        var reserved = (length * LogChartXAxisMinMaxReservedRatio);
        this.logChartXAxis.MinLimit = maxSeriesValueCount - length + reserved;
        this.logChartXAxis.MaxLimit = maxSeriesValueCount + reserved;
    }


    /// <summary>
    /// Horizontally scroll to start of log chart.
    /// </summary>
    public void ScrollToStartOfLogChart()
    {
        var minLimit = this.logChartXAxis.MinLimit ?? double.NaN;
        var maxLimit = this.logChartXAxis.MaxLimit ?? double.NaN;
        if (!double.IsFinite(minLimit) && !double.IsFinite(maxLimit))
            return;
        var length = (maxLimit - minLimit);
        var reserved = (length * LogChartXAxisMinMaxReservedRatio);
        this.logChartXAxis.MinLimit = -reserved;
        this.logChartXAxis.MaxLimit = length - reserved;
    }
    
    
    // Select color for series.
    SKColor SelectLogChartSeriesColor(string? propertyName)
    {
        // use existent color
        if (propertyName is not null && this.logChartSeriesColors.TryGetValue(propertyName, out var color))
            return color;
        
        // generate color pool
        if (this.logChartSeriesColorPool.IsEmpty())
        {
            this.logChartSeriesColorPool.AddRange(this.Application.EffectiveThemeMode switch
            {
                ThemeMode.Dark => LogChartSeriesColorsDark,
                _ => LogChartSeriesColorsLight,
            });
            this.logChartSeriesColorPool.Shuffle();
        }
        
        // select color
        var colorIndex = this.logChartSeriesColorPool.Count - 1;
        color = this.logChartSeriesColorPool[colorIndex];
        if (this.Application.IsDebugMode)
        {
            color.ToHsl(out var h, out var s, out var l);
            this.Logger.LogTrace("Select color (H: {h:f0}, S: {s:f0}, L: {l:f0}) for log chart series", h, s, l);
        }
        this.logChartSeriesColorPool.RemoveAt(colorIndex);
        if (propertyName is not null)
            this.logChartSeriesColors[propertyName] = color;
        return color;
    }


    // Setup items of menu for types of log chart.
    IList<MenuItem> SetupLogChartTypeMenuItems(ContextMenu menu)
    {
        var app = this.Application;
        var menuItems = new List<MenuItem>();
        foreach (var type in Enum.GetValues<LogChartType>())
        {
            if (type == LogChartType.None)
                continue;
            menuItems.Add(new MenuItem().Also(menuItem =>
            {
                menuItem.Click += (_, _) =>
                {
                    (this.DataContext as Session)?.LogChart.SetChartTypeCommand.TryExecute(type);
                    menu.Close();
                };
                var nameTextBlock = new Avalonia.Controls.TextBlock().Also(it =>
                {
                    it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding { Source = type, Converter = LogChartTypeNameConverter });
                    it.TextTrimming = TextTrimming.CharacterEllipsis;
                    it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                });
                var currentTypeTextBlock = new Avalonia.Controls.TextBlock().Also(it =>
                {
                    it.Opacity = app.FindResourceOrDefault<double>("Double/SessionView.LogChartTypeMenu.CurrentLogChartType.Opacity");
                    it.Bind(Avalonia.Controls.TextBlock.TextProperty, app.GetObservableString("SessionView.LogChartTypeMenu.CurrentLogChartType"));
                    it.TextTrimming = TextTrimming.CharacterEllipsis;
                    it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                    Grid.SetColumn(it, 2);
                });
                menuItem.Header = new Grid().Also(grid =>
                {
                    grid.ColumnDefinitions.Add(new(1, GridUnitType.Star)
                    {
                        SharedSizeGroup = "Name",
                    });
                    grid.ColumnDefinitions.Add(new(1, GridUnitType.Auto));
                    grid.ColumnDefinitions.Add(new(1, GridUnitType.Auto));
                    grid.Children.Add(nameTextBlock);
                    grid.Children.Add(new Separator().Also(it =>
                    {
                        it.Classes.Add("Dialog_Separator_Small");
                        it.Bind(IsVisibleProperty, new Binding
                        {
                            Path = nameof(IsVisible),
                            Source = currentTypeTextBlock,
                        });
                        Grid.SetColumn(it, 1);
                    }));
                    grid.Children.Add(currentTypeTextBlock);
                });
                menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
                {
                    icon.Classes.Add("MenuItem_Icon");
                    icon.Bind(Avalonia.Controls.Image.SourceProperty, new Binding { Source = type, Converter = LogChartTypeIconConverter.Outline });
                });
                menuItem.GetObservable(DataContextProperty).Subscribe(dataContext =>
                {
                    if (dataContext is not LogChartType currentType || type != currentType)
                    {
                        nameTextBlock.FontWeight = FontWeight.Normal;
                        currentTypeTextBlock.IsVisible = false;
                    }
                    else
                    {
                        nameTextBlock.FontWeight = FontWeight.Bold;
                        currentTypeTextBlock.IsVisible = true;
                    }
                });
            }));
        }
        Grid.SetIsSharedSizeScope(menu, true);
        return menuItems;
    }


    /// <summary>
    /// Open menu of types of log chart.
    /// </summary>
    public void ShowLogChartTypeMenu()
    {
        this.logChartTypeMenu.DataContext = (this.DataContext as Session)?.LogChart.ChartType;
        this.logChartTypeMenu.Open(this.logChartTypeButton);
    }
    
    
    // Update axes of log chart.
    void UpdateLogChartAxes()
    {
        if (this.areLogChartAxesReady)
            return;
        var app = this.Application;
        var axisFontSize = app.FindResourceOrDefault("Double/SessionView.LogChart.Axis.FontSize", 10.0);
        var axisWidth = app.FindResourceOrDefault("Double/SessionView.LogChart.Axis.Width", 2.0);
        var yAxisPadding = app.FindResourceOrDefault("Thickness/SessionView.LogChart.Axis.Padding.Y", default(Thickness)).Let(t => new Padding(t.Left, t.Top, t.Right, t.Bottom));
        var textBrush = app.FindResourceOrDefault("TextControlForeground", Brushes.Black);
        var crosshairBrush = app.FindResourceOrDefault("Brush/SessionView.LogChart.Axis.Crosshair", Brushes.Black);
        var crosshairWidth = app.FindResourceOrDefault("Double/SessionView.LogChart.Axis.Crosshair.Width", 1.0);
        var separatorBrush = app.FindResourceOrDefault("Brush/SessionView.LogChart.Axis.Separator", Brushes.Black);
        this.logChartYAxisCrosshairPaint = crosshairBrush.Let(brush =>
        {
            var color = brush.Color;
            return new SolidColorPaint(new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5)))
            {
                StrokeThickness = (float)crosshairWidth,
            };
        });
        this.logChartXAxis.Let(axis =>
        {
            axis.LabelsPaint = null;
            axis.TextSize = (float)axisFontSize;
            axis.ZeroPaint = null;
        });
        this.logChartYAxis.Let(axis =>
        {
            var textPaint = textBrush.Let(brush =>
            {
                var color = brush.Color;
                return new SolidColorPaint(new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5)));
            });
            axis.LabelsPaint = textPaint;
            axis.Padding = yAxisPadding;
            axis.SeparatorsPaint = separatorBrush.Let(brush =>
            {
                var color = brush.Color;
                return new SolidColorPaint
                {
                    Color = new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5)),
                    StrokeThickness = (float)this.FindResourceOrDefault("Double/DisplayableLogChartSeriesGenerator.Axis.Separator.Width", 1.0),
                    PathEffect = new DashEffect(new float[] { 3, 3 }),
                };
            });
            axis.TextSize = (float)axisFontSize;
            axis.ZeroPaint = new SolidColorPaint(textPaint.Color, (float)axisWidth);
        });
        this.areLogChartAxesReady = true;
    }


    // Update paints for log chart.
    void UpdateLogChartPaints()
    {
        (this.FindResource("ToolTipBackground") as ISolidColorBrush)?.Let(brush =>
        {
            var color = brush.Color;
            var skColor = new SKColor(color.R, color.G, color.B, (byte) (color.A * brush.Opacity + 0.5));
            this.SetValue(LogChartLegendBackgroundPaintProperty, new SolidColorPaint(skColor));
            this.SetValue(LogChartToolTipBackgroundPaintProperty, new SolidColorPaint(skColor));
        });
        (this.FindResource("ToolTipForeground") as ISolidColorBrush)?.Let(brush =>
        {
            var c = this.Application.CultureInfo.Name.Let(it =>
            {
                if (it.StartsWith("zh"))
                    return it.EndsWith("TW") ? '繁' : '简';
                return 'a';
            });
            var typeface = SKFontManager.Default.MatchCharacter(c);
            var color = brush.Color;
            var skColor = new SKColor(color.R, color.G, color.B, (byte) (color.A * brush.Opacity + 0.5));
            this.SetValue(LogChartLegendForegroundPaintProperty, new SolidColorPaint(skColor) { SKTypeface = typeface });
            this.SetValue(LogChartToolTipForegroundPaintProperty, new SolidColorPaint(skColor) { SKTypeface = typeface });
        });
    }
    
    
    // Update series of log chart.
    void UpdateLogChartSeries()
    {
        // clear current series
        this.logChartSeries.Clear();

        // check state
        if (this.DataContext is not Session session)
            return;
        var viewModel = session.LogChart;
        if (!viewModel.IsChartDefined)
            return;
        
        // setup axes
        this.UpdateLogChartAxes();
        this.updateLogChartYAxisLimitAction.Execute();
        
        // create series
        foreach (var series in viewModel.Series)
            this.logChartSeries.Add(this.CreateLogChartSeries(viewModel, series));
        ((int)this.logChart.AnimationsSpeed.TotalMilliseconds).Let(it => // [Workaround] Prevent animation interrupted unexpectedly
        {
            if (it > 100)
                this.SynchronizationContext.PostDelayed(() => this.logChart.CoreChart.Update(), 100);
            this.SynchronizationContext.PostDelayed(() => this.logChart.CoreChart.Update(), it);
        });
        
        // update zoom mode
        this.logChart.ZoomMode = viewModel.MaxSeriesValueCount > LogChartXAxisMinValueCount 
            ? ZoomAndPanMode.X 
            : ZoomAndPanMode.None;
    }
    
    
    // Update visibility of log chart panel.
    void UpdateLogChartPanelVisibility()
    { }
    
    
    // Update limit of X-axis.
    void UpdateLogChartXAxisLimit()
    {
        if (this.DataContext is not Session session)
            return;
        var viewModel = session.LogChart;
        if (!viewModel.IsChartDefined)
        {
            this.SetValue(IsLogChartHorizontallyZoomedProperty, false);
            return;
        }
        var axis = this.logChartXAxis;
        var minLimit = axis.MinLimit ?? double.NaN;
        var maxLimit = axis.MaxLimit ?? double.NaN;
        if (!double.IsFinite(minLimit) && !double.IsFinite(maxLimit))
        {
            this.SetValue(IsLogChartHorizontallyZoomedProperty, false);
            return;
        }
        var maxValueCount = viewModel.MaxSeriesValueCount;
        if (maxValueCount <= LogChartXAxisMinValueCount)
        {
            axis.MinLimit = null;
            axis.MaxLimit = null;
            this.SetValue(IsLogChartHorizontallyZoomedProperty, false);
            return;
        }
        var reserved = maxValueCount * LogChartXAxisMinMaxReservedRatio;
        var isSnappedToEdge = false;
        if (minLimit < 0.5)
        {
            minLimit = -reserved;
            axis.MinLimit = null;
            isSnappedToEdge = true;
            if (maxLimit < minLimit + LogChartXAxisMinValueCount)
            {
                maxLimit = minLimit + LogChartXAxisMinValueCount + 0.0001;
                axis.MaxLimit = maxLimit;
            }
        }
        if (maxLimit > maxValueCount - 0.5)
        {
            maxLimit = maxValueCount + reserved;
            axis.MaxLimit = null;
            isSnappedToEdge = true;
            if (minLimit > maxLimit - LogChartXAxisMinValueCount)
            {
                minLimit = maxLimit - LogChartXAxisMinValueCount - 0.0001;
                axis.MinLimit = minLimit;
            }
        }
        if (!isSnappedToEdge && (maxLimit - minLimit) < LogChartXAxisMinValueCount)
        {
            var center = (minLimit + maxLimit) / 2;
            minLimit = center - (LogChartXAxisMinValueCount / 2.0);
            maxLimit = center + (LogChartXAxisMinValueCount / 2.0);
            axis.MinLimit = minLimit;
            axis.MaxLimit = maxLimit;
        }
        this.SetValue(IsLogChartHorizontallyZoomedProperty, axis.MinLimit.HasValue || axis.MaxLimit.HasValue);
    }
    
    
    // Update limit of Y-axis.
    void UpdateLogChartYAxisLimit()
    {
        if (this.DataContext is not Session session)
            return;
        var viewModel = session.LogChart;
        if (!viewModel.IsChartDefined)
            return;
        var minLimit = viewModel.MinSeriesValue?.Value ?? double.NaN;
        var maxLimit = viewModel.MaxSeriesValue?.Value ?? double.NaN;
        if (double.IsFinite(minLimit) && double.IsFinite(maxLimit))
        {
            var range = maxLimit - minLimit;
            var reserved = Math.Max(0.25, range * LogChartYAxisMinMaxReservedRatio);
            if (maxLimit < (double.MaxValue - 1 - reserved) && minLimit > (double.MinValue + 1 + reserved))
            {
                if (minLimit >= 0)
                {
                    this.logChartYAxis.MinLimit = -reserved;
                    this.logChartYAxis.MaxLimit = maxLimit + reserved;
                }
                else if (maxLimit <= 0)
                {
                    this.logChartYAxis.MinLimit = minLimit - reserved;
                    this.logChartYAxis.MaxLimit = reserved;
                }
                else
                {
                    this.logChartYAxis.MinLimit = minLimit - reserved;
                    this.logChartYAxis.MaxLimit = maxLimit + reserved;
                }
                return;
            }
        }
        this.logChartYAxis.MinLimit = null;
        this.logChartYAxis.MaxLimit = null;
    }
}