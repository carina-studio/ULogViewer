using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.Controls;

partial class SessionView
{
    /// <summary>
    /// Define <see cref="LogChartLegendBackgroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartLegendBackgroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartLegendBackgroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartLegendForegroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartLegendForegroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartLegendForegroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartToolTipBackgroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartToolTipBackgroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartToolTipBackgroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartToolTipForegroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<IPaint<SkiaSharpDrawingContext>> LogChartToolTipForegroundPaintProperty = AvaloniaProperty.Register<SessionView, IPaint<SkiaSharpDrawingContext>>(nameof(LogChartToolTipForegroundPaint), new SolidColorPaint());
    
    
    // Constants.
    const int RecentlyUsedLogChartSeriesColorCount = 8;
    const int LogChartXAxisMinMaxUpdatingDelay = 100;
    const double LogChartXAxisMinMaxReservedRatio = 0.01;
    const double LogChartYAxisMinMaxReservedRatio = 0.05;


    // Fields.
    bool areLogChartAxesReady;
    INotifyCollectionChanged? attachedRawLogChartSeries;
    readonly HashSet<INotifyCollectionChanged> attachedRawLogChartSeriesValues = new();
    bool isSyncingLogChartPanelSize;
    readonly CartesianChart logChart;
    readonly RowDefinition logChartGridRow;
    readonly ObservableList<ISeries> logChartSeries = new();
    readonly Dictionary<string, SKColor> logChartSeriesColors = new();
    readonly Axis logChartXAxis = new()
    {
        IsVisible = false,
    };
    readonly Axis logChartYAxis = new()
    {
        CrosshairSnapEnabled = true,
    };
    IPaint<SkiaSharpDrawingContext>? logChartYAxisCrosshairPaint;
    readonly Queue<SKColor> recentlyUsedLogChartSeriesColors = new(RecentlyUsedLogChartSeriesColorCount);
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
        var color = SelectLogChartSeriesColor(series.LogProperty?.Name);
        
        // create series
        return viewModel.ChartType switch
        {
            LogChartType.CategoryBars 
                or LogChartType.ValueBars => new ColumnSeries<DisplayableLogChartSeriesValue>
            {
                Fill = new SolidColorPaint(color),
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.LogProperty?.DisplayName,
                Padding = 1,
                Rx = 0,
                Ry = 0,
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.LogProperty?.DisplayName}: {point.PrimaryValue}";
                },
                Values = series.Values,
            },
            LogChartType.ValueStackedAreas => new StackedAreaSeries<DisplayableLogChartSeriesValue>
            {
                Fill = new SolidColorPaint(color.WithAlpha((byte)(color.Alpha >> 3))),
                GeometryFill = new SolidColorPaint(color),
                GeometrySize = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Point.Size", 6.0),
                GeometryStroke = null,
                LineSmoothness = 0,
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.LogProperty?.DisplayName,
                Stroke = new SolidColorPaint(color, (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Width", 2.0)),
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.LogProperty?.DisplayName}: {point.PrimaryValue}";
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
                Name = series.LogProperty?.DisplayName,
                Padding = 1,
                Rx = 0,
                Ry = 0,
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.LogProperty?.DisplayName}: {point.PrimaryValue}";
                },
                Values = series.Values,
            },
            _ => new LineSeries<DisplayableLogChartSeriesValue>
            {
                Fill = null,
                GeometryFill = new SolidColorPaint(color),
                GeometrySize = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Point.Size", 6.0),
                GeometryStroke = null,
                LineSmoothness = 0,
                Mapping = (value, point) =>
                {
                    if (value.Value.HasValue)
                        point.PrimaryValue = value.Value.Value;
                    point.SecondaryValue = point.Context.Entity.EntityIndex;
                },
                Name = series.LogProperty?.DisplayName,
                Stroke = new SolidColorPaint(color, (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Width", 2.0)),
                TooltipLabelFormatter = point =>
                {
                    var value = series.Values[point.Context.Entity.EntityIndex];
                    return $"{value.Label ?? series.LogProperty?.DisplayName}: {point.PrimaryValue}";
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
        foreach (var values in this.attachedRawLogChartSeriesValues)
            values.CollectionChanged -= this.OnRawLogChartSeriesValuesChanged;
        this.attachedRawLogChartSeries = null;
        this.attachedRawLogChartSeriesValues.Clear();
        this.logChartSeries.Clear();
        this.updateLogChartXAxisLimitAction.Execute();
    }
    
    
    // Generate color for log chart series.
    SKColor GenerateRandomSKColor(Random random) => this.Application.EffectiveThemeMode switch
    {
        ThemeMode.Dark => new((byte) (random.Next(180) + 60), (byte) (random.Next(180) + 60), (byte) (random.Next(180) + 60)),
        _ => new((byte) (random.Next(180) + 20), (byte) (random.Next(180) + 20), (byte) (random.Next(180) + 20)),
    };


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


    // Called when pointer entered or leave the chart.
    void OnLogChartPointerOverChanged(bool isPointerOver)
    {
        this.logChartYAxis.CrosshairPaint = isPointerOver && this.DataContext is Session session
            ? session.LogChart.ChartType switch
            {
                LogChartType.ValueStackedAreas
                    or LogChartType.ValueStackedBars => null,
                _ => this.logChartYAxisCrosshairPaint,
            }
            : null;
    }


    // Called when pointer down on data points in log chart.
    void OnLogChartDataPointerDown(IEnumerable<ChartPoint> points)
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
            case nameof(LogChartViewModel.IsChartDefined):
                this.UpdateLogChartSeries();
                break;
            case nameof(LogChartViewModel.IsPanelVisible):
                this.UpdateLogChartPanelVisibility();
                break;
            case nameof(LogChartViewModel.MaxSeriesValue):
            case nameof(LogChartViewModel.MinSeriesValue):
                this.updateLogChartYAxisLimitAction.Schedule();
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
                    {
                        if (series[i].Values is INotifyCollectionChanged notifyCollectionChanged
                            && this.attachedRawLogChartSeriesValues.Add(notifyCollectionChanged))
                        {
                            notifyCollectionChanged.CollectionChanged += this.OnRawLogChartSeriesValuesChanged;
                        }
                        this.logChartSeries.Insert(startIndex + i, this.CreateLogChartSeries(viewModel, series[i]));
                    }
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogChartSeries>().Let(series =>
                {
                    for (var i = series.Count - 1; i >= 0; --i)
                    {
                        if (series[i].Values is INotifyCollectionChanged notifyCollectionChanged
                            && this.attachedRawLogChartSeriesValues.Remove(notifyCollectionChanged))
                        {
                            notifyCollectionChanged.CollectionChanged -= this.OnRawLogChartSeriesValuesChanged;
                        }
                    }
                    this.logChartSeries.RemoveRange(e.OldStartingIndex, series.Count);
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                this.UpdateLogChartSeries();
                break;
            default:
                throw new NotSupportedException();
        }
    }
    
    
    // Called when one of values of series of log chart has been changed.
    void OnRawLogChartSeriesValuesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        this.updateLogChartXAxisLimitAction.Schedule(LogChartXAxisMinMaxUpdatingDelay);
    
    
    // Select color for series.
    SKColor SelectLogChartSeriesColor(string? propertyName)
    {
        if (propertyName is not null && this.logChartSeriesColors.TryGetValue(propertyName, out var color))
            return color;
        var random = new Random();
        while (true)
        {
            color = this.GenerateRandomSKColor(random);
            var isColorSelected = true;
            foreach (var ruColor in this.recentlyUsedLogChartSeriesColors)
            {
                var rDiff = Math.Abs(color.Red - ruColor.Red);
                var gDiff = Math.Abs(color.Green - ruColor.Green);
                var bDiff = Math.Abs(color.Blue - ruColor.Blue);
                var closingCount = 0;
                if (rDiff <= 0)
                    ++closingCount;
                if (gDiff <= 0)
                    ++closingCount;
                if (bDiff <= 0)
                    ++closingCount;
                if (closingCount >= 2)
                {
                    isColorSelected = false;
                    break;
                }
            }
            if (!isColorSelected)
                continue;
            foreach (var existingColor in this.logChartSeriesColors.Values)
            {
                var rDiff = Math.Abs(color.Red - existingColor.Red);
                var gDiff = Math.Abs(color.Green - existingColor.Green);
                var bDiff = Math.Abs(color.Blue - existingColor.Blue);
                if (rDiff <= 10 && gDiff <= 10 && bDiff <= 10)
                {
                    isColorSelected = false;
                    break;
                }
            }
            if (!isColorSelected)
                continue;
            while (this.recentlyUsedLogChartSeriesColors.Count >= RecentlyUsedLogChartSeriesColorCount)
                this.recentlyUsedLogChartSeriesColors.Dequeue();
            this.recentlyUsedLogChartSeriesColors.Enqueue(color);
            if (propertyName is not null)
                this.logChartSeriesColors[propertyName] = color;
            return color;
        }
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
            var textPaint = textBrush.Let(brush =>
            {
                var color = brush.Color;
                return new SolidColorPaint(new(color.R, color.G, color.B, (byte) (color.A * brush.Opacity + 0.5)));
            });
            axis.LabelsPaint = textPaint;
            axis.TextSize = (float)axisFontSize;
            axis.ZeroPaint = new SolidColorPaint(textPaint.Color, (float)axisWidth);
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
        // detach from current series
        foreach (var values in this.attachedRawLogChartSeriesValues)
            values.CollectionChanged -= this.OnRawLogChartSeriesValuesChanged;
        this.attachedRawLogChartSeriesValues.Clear();
        
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
        this.updateLogChartXAxisLimitAction.Execute();
        this.updateLogChartYAxisLimitAction.Execute();
        
        // create series
        foreach (var series in viewModel.Series)
        {
            if (series.Values is INotifyCollectionChanged notifyCollectionChanged
                && this.attachedRawLogChartSeriesValues.Add(notifyCollectionChanged))
            {
                notifyCollectionChanged.CollectionChanged += this.OnRawLogChartSeriesValuesChanged;
            }
            this.logChartSeries.Add(this.CreateLogChartSeries(viewModel, series));
        }
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
            return;
        var maxSeriesValueCount = 0;
        foreach (var series in viewModel.Series)
            maxSeriesValueCount = Math.Max(maxSeriesValueCount, series.Values.Count);
        var reserved = Math.Max(1, maxSeriesValueCount * LogChartXAxisMinMaxReservedRatio);
        this.logChartXAxis.MinLimit = -reserved;
        this.logChartXAxis.MaxLimit = maxSeriesValueCount + reserved;
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
            var reserved = range * LogChartYAxisMinMaxReservedRatio;
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