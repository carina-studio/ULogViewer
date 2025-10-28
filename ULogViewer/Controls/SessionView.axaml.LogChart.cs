using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
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
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
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
using System.Text;
using CarinaStudio.AppSuite.Media;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView.Drawing.Layouts;
using System.Globalization;
using System.Threading.Tasks;
using DrawingContext = LiveChartsCore.Drawing.DrawingContext;

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
    public static readonly StyledProperty<Paint> LogChartLegendBackgroundPaintProperty = AvaloniaProperty.Register<SessionView, Paint>(nameof(LogChartLegendBackgroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartLegendForegroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<Paint> LogChartLegendForegroundPaintProperty = AvaloniaProperty.Register<SessionView, Paint>(nameof(LogChartLegendForegroundPaint), new SolidColorPaint());
    /// <summary>
    /// <see cref="IValueConverter"/> to convert <see cref="LogChartType"/> to readable name.
    /// </summary>
    public static readonly IValueConverter LogChartTypeNameConverter = new AppSuite.Converters.EnumConverter(IAppSuiteApplication.CurrentOrNull, typeof(LogChartType));
    /// <summary>
    /// Define <see cref="LogChartToolTipBackgroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<Paint> LogChartToolTipBackgroundPaintProperty = AvaloniaProperty.Register<SessionView, Paint>(nameof(LogChartToolTipBackgroundPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartToolTipBorderPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<Paint> LogChartToolTipBorderPaintProperty = AvaloniaProperty.Register<SessionView, Paint>(nameof(LogChartToolTipBorderPaint), new SolidColorPaint());
    /// <summary>
    /// Define <see cref="LogChartToolTipBorderThickness"/> property.
    /// </summary>
    public static readonly StyledProperty<double> LogChartToolTipBorderThicknessProperty = AvaloniaProperty.Register<SessionView, double>(nameof(LogChartToolTipBorderThickness), 0.0);
    /// <summary>
    /// Define <see cref="LogChartToolTipForegroundPaint"/> property.
    /// </summary>
    public static readonly StyledProperty<Paint> LogChartToolTipForegroundPaintProperty = AvaloniaProperty.Register<SessionView, Paint>(nameof(LogChartToolTipForegroundPaint), new SolidColorPaint());
    
    
    // Legend of log chart.
    class LogChartLegend(SessionView view) : IChartLegend
    {
        // Fields.
        StackLayout? container;
        DrawnTask? containerDrawableTask;
        
        // Draw.
        public void Draw(Chart chart)
        {
            var container = this.container;
            if (container is null)
                return;
            var position = chart.GetLegendPosition();
            container.X = position.X;
            container.Y = position.Y;
            if (this.containerDrawableTask is null)
            {
                this.containerDrawableTask = chart.Canvas.AddGeometry(container);
                this.containerDrawableTask.ZIndex = LogChartLegendZIndex;
            }
        }
        
        // Hide.
        public void Hide(Chart chart)
        {
            if (this.containerDrawableTask is not null)
            {
                chart.Canvas.RemovePaintTask(this.containerDrawableTask);
                this.containerDrawableTask = null;
            }
        }

        // Measure.
        public LvcSize Measure(Chart chart)
        {
            // create container
            var sessionView = view;
            var logChart = sessionView.logChart;
            var container = this.container ?? new StackLayout().Also(it =>
            {
                var padding = sessionView.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogChart.Legend.Padding");
                it.HorizontalAlignment = Align.Start;
                it.Orientation = ContainerOrientation.Vertical;
                it.Padding = new(padding.Left, padding.Top, padding.Right, padding.Bottom);
                it.VerticalAlignment = Align.Middle;
                this.container = it;
            });
            
            // remove old visuals
            var containerChildViews = container.Children;
            containerChildViews.Clear();
            
            // add visuals
            var itemMargin = sessionView.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogChart.Legend.Item.Margin").Let(it =>
                new Padding(it.Left, it.Top, it.Right, it.Bottom));
            foreach (var series in chart.Series.Where(it => it.IsVisible && it.IsVisibleAtLegend))
            {
                var sketch = series.GetMiniatureGeometry(null);
                containerChildViews.Add(new StackLayout().Also(it =>
                {
                    it.Children.Add((IDrawnElement<SkiaSharpDrawingContext>)sketch);
                    it.Children.Add(new LabelGeometry().Also(it =>
                    {
                        it.HorizontalAlign = Align.Start;
                        it.Padding = itemMargin;
                        it.Paint = logChart.LegendTextPaint;
                        if (series.Tag is DisplayableLogChartSeries displayableLogChartSeries
                            && displayableLogChartSeries.Source is not null)
                        {
                            it.Text = sessionView.GetLogChartSeriesDisplayName(displayableLogChartSeries.Source);
                        }
                        it.TextSize = (float)logChart.LegendTextSize;
                        it.VerticalAlign = Align.Start;
                    }));
                    it.Orientation = ContainerOrientation.Horizontal;
                }));
            }
            
            // measure
            return container.Measure();
        }
    }
    
    
    // Tool tip of log chart.
    class LogChartToolTip(SessionView view, bool isXAxisInverted) : IChartTooltip
    {
        // Fields.
        Container<RoundedRectangleGeometry>? container;
        DrawnTask? containerDrawableTask;
        StackLayout? itemsContainer;

        // Hide.
        public void Hide(Chart chart)
        {
            if (this.containerDrawableTask is not null)
            {
                chart.Canvas.RemovePaintTask(this.containerDrawableTask);
                this.containerDrawableTask = null;
            }
            this.itemsContainer?.Children.Clear();
        }
        
        // Whether X-axis is inverted or not.
        public bool IsXAxisInverted { get; } = isXAxisInverted;
        
        // Show
        public void Show(IEnumerable<ChartPoint> foundPoints, Chart chart)
        {
            // creat container
            var sessionView = view;
            var logChart = sessionView.logChart;
            var container = this.container ?? new Container<RoundedRectangleGeometry>().Also(container =>
            {
                var cornerRadius = sessionView.FindResourceOrDefault<CornerRadius>("CornerRadius/ToolTip");
                var geometry = container.Geometry;
                geometry.BorderRadius = new(cornerRadius.TopLeft, cornerRadius.BottomLeft);
                geometry.Fill = logChart.TooltipBackgroundPaint;
                geometry.Stroke = sessionView.LogChartToolTipBorderPaint;
                geometry.Width = (float)sessionView.LogChartToolTipBorderThickness;
                this.container = container;
            });
            var itemsContainer = this.itemsContainer ?? new StackLayout().Also(itemsContainer =>
            {
                var padding = view.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogChart.ToolTip.Padding");
                //container.ClippingMode = ClipMode.None;
                itemsContainer.HorizontalAlignment = Align.Start;
                itemsContainer.Orientation = ContainerOrientation.Vertical;
                itemsContainer.Padding = new(padding.Left, padding.Top, padding.Right, padding.Bottom);
                itemsContainer.VerticalAlignment = Align.Middle;
                container.Content = itemsContainer;
                this.itemsContainer = itemsContainer;
            });
            
            // remove unneeded child views
            var containerChildViews = itemsContainer.Children;
            containerChildViews.Clear();
            
            // show information of data points
            var isFirstPoint = true;
            var itemMargin = sessionView.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogChart.ToolTip.Item.Margin");
            foreach (var point in foundPoints)
            {
                var series = (ICartesianSeries)point.Context.Series;
                var sketch = series.GetMiniatureGeometry(null);
                if (isFirstPoint)
                {
                    isFirstPoint = false;
                    if ((sessionView.DataContext as Session)?.LogChart.ChartType != LogChartType.ValueStatisticBars)
                    {
                        containerChildViews.Add(new LabelGeometry().Also(it =>
                        {
                            //it.ClippingMode = ClipMode.None;
                            it.HorizontalAlign = Align.Start;
                            it.Paint = logChart.TooltipTextPaint;
                            it.Text = sessionView.GetLogChartXToolTipLabel(point, this.IsXAxisInverted);
                            it.TextSize = (float)logChart.TooltipTextSize;
                            it.VerticalAlign = Align.Start;
                        }));
                    }
                }
                containerChildViews.Add(new StackLayout().Also(pointContainer =>
                {
                    pointContainer.Children.Add((IDrawnElement<SkiaSharpDrawingContext>)sketch);
                    pointContainer.Children.Add(new LabelGeometry().Also(it =>
                    {
                        //it.ClippingMode = ClipMode.None;
                        it.HorizontalAlign = Align.Start;
                        it.Padding = new(itemMargin.Left, itemMargin.Top, itemMargin.Right, itemMargin.Bottom);
                        it.Paint = logChart.TooltipTextPaint;
                        it.Text = (series.Tag as DisplayableLogChartSeries)?.Let(it => 
                            sessionView.GetLogChartYToolTipLabel(it, point, true)) 
                                  ?? point.Coordinate.PrimaryValue.ToString(CultureInfo.InvariantCulture);
                        it.TextSize = (float)logChart.TooltipTextSize;
                        it.VerticalAlign = Align.Start;
                    }));
                    pointContainer.HorizontalAlignment = Align.Middle;
                    pointContainer.Orientation = ContainerOrientation.Horizontal;
                    pointContainer.VerticalAlignment = Align.Middle;
                }));
            }
            
            // show container
            var chartSize = chart.View.ControlSize;
            var offset = (float)sessionView.FindResourceOrDefault<double>("Double/SessionView.LogChart.ToolTip.Offset");
            var containerSize = container.Measure();
            var containerPosition = foundPoints.GetTooltipLocation(new(containerSize.Width, containerSize.Height + offset + offset), chart);
            if (containerPosition.Y < -offset)
            {
                var shift = (-offset - containerPosition.Y);
                var maxShift = Math.Max(0, chartSize.Height + offset - (containerPosition.Y + containerSize.Height));
                containerPosition.Y += Math.Min(shift, maxShift);
            }
            else if (containerPosition.Y + containerSize.Height > chartSize.Height + offset)
                containerPosition.Y = chartSize.Height + offset - containerSize.Height;
            container.X = containerPosition.X;
            container.Y = containerPosition.Y + offset;
            if (this.containerDrawableTask is null)
            {
                this.containerDrawableTask = chart.Canvas.AddGeometry(container);
                this.containerDrawableTask.ZIndex = LogChartToolTipZIndex;
            }
        }
    }


    // Extended SolidColorPaint.
    class SolidColorPaintEx(SKColor color) : SolidColorPaint(color)
    {
        // Blend mode.
        public SKBlendMode BlendMode { get; init; } = SKBlendMode.Overlay;

        /// <inheritdoc/>
        protected override void OnPaintStarted(DrawingContext drawingContext, IDrawnElement? drawnElement)
        {
            base.OnPaintStarted(drawingContext, drawnElement);
            if (drawingContext is SkiaSharpDrawingContext skiaDrawingContext && skiaDrawingContext.ActiveSkiaPaint is { } skPaint)
                skPaint.BlendMode = this.BlendMode;
        }
    }
    
    
    // Constants.
    const double LogBarChartXCoordinateScaling = 1.3;
    const long DurationToDropClickEvent = 500;
    const double PointerDistanceToDropClickEvent = 5;
    const int LogChartLegendZIndex = 89999;
    const int LogChartToolTipZIndex = 99999;
    const double LogChartXAxisMinRange = 10.0;
    const double LogChartXRangeWhenDoubleClick = 20.0;
    const double LogChartYAxisMinMaxReservedRatio = 0.05;
    const int ResumeLogChartAnimationDelay = 500;
    
    
    // Static fields.
    static SKTypeface? InterSKTypeFace;
    static readonly SettingKey<bool> IsLogChartTutorialShownKey = new("SessionView.IsLogChartTutorialShown", false);
    static readonly SKColor[] LogChartSeriesColorsDark =
    [
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
    ];
    static readonly SKColor[] LogChartSeriesColorsLight =
    [
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
    ];
    // ReSharper disable IdentifierTypo
    static SKTypeface? NotoSansSCSKTypeFace;
    static SKTypeface? NotoSansTCSKTypeFace;
    // ReSharper restore IdentifierTypo
    static readonly SettingKey<bool> PromptWhenMaxTotalLogSeriesValueCountReachedKey = new("SessionView.PromptWhenMaxTotalLogSeriesValueCountReached", true);


    // Fields.
    bool areLogChartAxesReady;
    INotifyCollectionChanged? attachedRawLogChartSeries;
    INotifyCollectionChanged? attachedRawVisibleLogChartSeries;
    bool isLogChartDoubleTapped;
    bool isPointerPressedOnLogChart;
    bool isSyncingLogChartPanelSize;
    readonly CartesianChart logChart;
    readonly RowDefinition logChartGridRow;
    Cursor? logChartHandCursor;
    IDisposable? logChartHandCursorValueToken;
    ChartPoint[] logChartPointerDownData = [];
    Point? logChartPointerDownPosition;
    readonly Stopwatch logChartPointerDownWatch = new();
    readonly ObservableList<ISeries> logChartSeries = new();
    readonly List<SKColor> logChartSeriesColorPool = new();
    readonly Dictionary<string, SKColor> logChartSeriesColors = new();
    LogChartToolTip? logChartToolTip;
    readonly ToggleButton logChartTypeButton;
    readonly ContextMenu logChartTypeMenu;
    readonly Axis logChartXAxis = new()
    {
        CrosshairSnapEnabled = true,
    };
    (double?, double?)? logChartXAxisLimitToKeep;
    double logChartXCoordinateScaling = 1.0;
    readonly Axis logChartYAxis = new()
    {
        CrosshairSnapEnabled = true,
    };
    Paint? logChartYAxisCrosshairPaint;
    readonly ScheduledAction startLogChartAnimationsAction;
    readonly ScheduledAction updateLogChartXAxisLimitAction;
    readonly ScheduledAction updateLogChartYAxisLimitAction;
    ToggleButton? visibleLogChartSeriesButton;
    ContextMenu? visibleLogChartSeriesMenu;
    
    
    // Attach to view-model of log chart.
    void AttachToLogChart(LogChartViewModel viewModel)
    {
        // add handlers
        viewModel.PropertyChanged += this.OnLogChartViewModelPropertyChanged;
        this.AttachToRawLogChartSeries(viewModel.Series, false);
        this.AttachToRawVisibleLogChartSeries(viewModel.VisibleSeries, true);
        (viewModel.SeriesSources as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnLogChartSeriesSourcesChanged);
        (viewModel.VisibleSeriesSources as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnVisibleLogChartSeriesSourcesChanged);
        
        // check data count
        if (viewModel.IsMaxTotalSeriesValueCountReached)
            _ = this.PromptForMaxLogChartSeriesValueCountReached();
        
        // update log chart
        this.areLogChartAxesReady = false;
        this.UpdateLogChartAxes();
        this.UpdateLogChartPanelVisibility();
    }
    
    
    // Attach to given raw series of log chart.
    void AttachToRawLogChartSeries(IList<DisplayableLogChartSeries> rawSeries, bool updateSeries)
    {
        this.DetachFromRawLogChartSeries(updateSeries);
        this.attachedRawLogChartSeries = rawSeries as INotifyCollectionChanged;
        if (this.attachedRawLogChartSeries is not null)
            this.attachedRawLogChartSeries.CollectionChanged += this.OnRawLogChartSeriesChanged;
        if (updateSeries)
            this.UpdateLogChartSeries();
    }
    
    
    // Attach to given visible raw series of log chart.
    void AttachToRawVisibleLogChartSeries(IList<DisplayableLogChartSeries> rawSeries, bool updateSeries)
    {
        this.DetachFromRawVisibleLogChartSeries(updateSeries);
        this.attachedRawVisibleLogChartSeries = rawSeries as INotifyCollectionChanged;
        if (this.attachedRawVisibleLogChartSeries is not null)
            this.attachedRawVisibleLogChartSeries.CollectionChanged += this.OnRawVisibleLogChartSeriesChanged;
        if (updateSeries)
            this.UpdateLogChartSeries();
    }
    
    
    // Create single series of log chart.
    ISeries CreateLogChartSeries(LogChartViewModel viewModel, DisplayableLogChartSeries series)
    {
        // select color
        var seriesColor = SelectLogChartSeriesColor(series.Source?.PropertyName);
        var overlappedSeriesColor = seriesColor.Let(it =>
        {
            it.ToHsl(out var h, out var s, out var l);
            return this.Application.EffectiveThemeMode switch
            {
                ThemeMode.Dark => SKColor.FromHsl(h, s, l * 0.5f),
                _ => SKColor.FromHsl(h, s, Math.Min(100, l * 2)),
            };
        });
        
        // load resources
        var backgroundColor = this.Application.FindResourceOrDefault<ISolidColorBrush>("Brush/WorkingArea.Background", Brushes.Black).Let(it =>
        {
            var color = it.Color;
            return new SKColor(color.R, color.G, color.B, (byte)(color.A * it.Opacity + 0.5));
        });
        var geometrySize = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Point.Size", 5.0);
        var geometryBorderWidth = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Point.Border.Width", 1.0);
        var lineWidth = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.LineSeries.Width", 1.0);
        var colorBlendingMode = this.Application.EffectiveThemeMode switch
        {
            ThemeMode.Dark => SKBlendMode.Screen,
            _ => SKBlendMode.Multiply,
        };
        
        // create series
        var chartType = viewModel.ChartType;
        var isInverted = viewModel.IsXAxisInverted;
        switch (chartType)
        {
            case LogChartType.ValueStatisticBars:
            case LogChartType.ValueBars:
                this.logChartXCoordinateScaling = LogBarChartXCoordinateScaling;
                return new ColumnSeries<DisplayableLogChartSeriesValue?>
                {
                    AnimationsSpeed = this.SelectLogChartSeriesAnimationSpeed(viewModel),
                    Fill = new SolidColorPaint(seriesColor),
                    Mapping = (value, index) => new(index * LogBarChartXCoordinateScaling, value!.Value),
                    Name = series.Source?.Let(source => this.GetLogChartSeriesDisplayName(source)),
                    Padding = 0.5,
                    Rx = 0,
                    Ry = 0,
                    Tag = series,
                    Values = (IReadOnlyList<DisplayableLogChartSeriesValue?>)(isInverted ? series.Values.Reverse() : series.Values),
                    XToolTipLabelFormatter = chartType == LogChartType.ValueStatisticBars ? null : this.GetLogChartXToolTipLabel,
                    YToolTipLabelFormatter = p => this.GetLogChartYToolTipLabel(series, p, false),
                };
            case LogChartType.ValueStackedAreas:
            case LogChartType.ValueStackedAreasWithDataPoints:
                this.logChartXCoordinateScaling = 1.0;
                return new StackedAreaSeries<DisplayableLogChartSeriesValue?>
                {
                    AnimationsSpeed = this.SelectLogChartSeriesAnimationSpeed(viewModel),
                    Fill = new SolidColorPaint(seriesColor),
                    GeometryFill = chartType switch
                    {
                        LogChartType.ValueStackedAreasWithDataPoints => new SolidColorPaint(seriesColor),
                        _ => null,
                    },
                    GeometrySize = geometrySize,
                    GeometryStroke = chartType switch
                    {
                        LogChartType.ValueStackedAreasWithDataPoints => new SolidColorPaint(backgroundColor, geometryBorderWidth)
                        {
                            IsAntialias = true,
                        },
                        _ => null,
                    },
                    LineSmoothness = 0,
                    Mapping = (value, index) => new(index, value!.Value),
                    Name = series.Source?.Let(source => this.GetLogChartSeriesDisplayName(source)),
                    Stroke = new SolidColorPaint(overlappedSeriesColor, lineWidth)
                    {
                        IsAntialias = true,
                    },
                    Tag = series,
                    Values = (IReadOnlyList<DisplayableLogChartSeriesValue?>)(isInverted ? series.Values.Reverse() : series.Values),
                    XToolTipLabelFormatter = this.GetLogChartXToolTipLabel,
                    YToolTipLabelFormatter = p => this.GetLogChartYToolTipLabel(series, p, false),
                };
            case LogChartType.ValueStackedBars:
                this.logChartXCoordinateScaling = LogBarChartXCoordinateScaling;
                return new StackedColumnSeries<DisplayableLogChartSeriesValue?>
                {
                    AnimationsSpeed = this.SelectLogChartSeriesAnimationSpeed(viewModel),
                    Fill = new SolidColorPaint(seriesColor),
                    Mapping = (value, index) => new(index * LogBarChartXCoordinateScaling, value!.Value),
                    Name = series.Source?.Let(source => this.GetLogChartSeriesDisplayName(source)),
                    Padding = 0,
                    Rx = 0,
                    Ry = 0,
                    Tag = series,
                    Values = (IReadOnlyList<DisplayableLogChartSeriesValue?>)(isInverted ? series.Values.Reverse() : series.Values),
                    XToolTipLabelFormatter = this.GetLogChartXToolTipLabel,
                    YToolTipLabelFormatter = p => this.GetLogChartYToolTipLabel(series, p, false),
                };
            default:
                this.logChartXCoordinateScaling = 1.0;
                return new LineSeries<DisplayableLogChartSeriesValue?>
                {
                    AnimationsSpeed = this.SelectLogChartSeriesAnimationSpeed(viewModel),
                    Fill = chartType switch
                    {
                        LogChartType.ValueAreas
                            or LogChartType.ValueAreasWithDataPoints => new SolidColorPaintEx(seriesColor.WithAlpha((byte)(seriesColor.Alpha * 0.8)))
                            {
                                BlendMode = colorBlendingMode,
                            },
                        _ => null,
                    },
                    GeometryFill = chartType switch
                    {
                        LogChartType.ValueAreasWithDataPoints
                            or LogChartType.ValueCurvesWithDataPoints
                            or LogChartType.ValueLinesWithDataPoints => new SolidColorPaint(seriesColor),
                        _ => null,
                    },
                    GeometrySize = geometrySize,
                    GeometryStroke = chartType switch
                    {
                        LogChartType.ValueAreasWithDataPoints
                            or LogChartType.ValueCurvesWithDataPoints
                            or LogChartType.ValueLinesWithDataPoints => new SolidColorPaint(backgroundColor, geometryBorderWidth)
                            {
                                IsAntialias = true,
                            },
                        _ => null,
                    },
                    LineSmoothness = chartType switch
                    {
                        LogChartType.ValueCurves
                            or LogChartType.ValueCurvesWithDataPoints => 1,
                        _ => 0,
                    },
                    Mapping = (value, index) => new(index, value!.Value),
                    Name = series.Source?.Let(source => this.GetLogChartSeriesDisplayName(source)),
                    Stroke = chartType switch
                    {
                        LogChartType.ValueAreas
                            or LogChartType.ValueAreasWithDataPoints => new SolidColorPaint(overlappedSeriesColor, lineWidth)
                            {
                                IsAntialias = true,
                            },
                        _ => new SolidColorPaint(seriesColor, lineWidth)
                        {
                            IsAntialias = true,
                        },
                    },
                    Tag = series,
                    Values = (IReadOnlyList<DisplayableLogChartSeriesValue?>)(isInverted ? series.Values.Reverse() : series.Values),
                    XToolTipLabelFormatter = this.GetLogChartXToolTipLabel,
                    YToolTipLabelFormatter = p => this.GetLogChartYToolTipLabel(series, p, false),
                };
        }
    }


    // Create menu item for visible series of log chart.
    MenuItem CreateVisibleLogChartSeriesMenuItem(DisplayableLogChartSeriesSource source) => new MenuItem().Also(it =>
    {
        it.Click += (_, e) =>
        {
            e.Handled = true;
            (this.DataContext as Session)?.LogChart.ToggleVisibleSeriesSourceCommand.TryExecute(source);
        };
        it.DataContext = source;
        it.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.IsVisible = (this.DataContext as Session)?.LogChart.VisibleSeriesSources.Contains(source) == true;
            icon.BindToResource(Avalonia.Controls.Image.SourceProperty, this, "Image/Icon.Checked.Thin");
        });
        it.Header = new Grid().Also(grid =>
        {
            grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new(1, GridUnitType.Auto));
            grid.Children.Add(new Avalonia.Controls.TextBlock().Also(it =>
            {
                it.Text = this.GetLogChartSeriesDisplayName(source);
                it.TextTrimming = TextTrimming.CharacterEllipsis;
            }));
            grid.Children.Add(new Avalonia.Controls.TextBlock().Also(it =>
            {
                Grid.SetColumn(it, 1);
                it.HorizontalAlignment = HorizontalAlignment.Right;
                it.Margin = this.FindResourceOrDefault<Thickness>("Thickness/LogProfileEditorDialog.VisibleLogPropertyListBox.Name.Margin");
                it.Opacity = this.FindResourceOrDefault("Double/LogProfileEditorDialog.VisibleLogPropertyListBox.Name.Opacity", 0.5);
                it.Text = $"({source.PropertyName})";
                it.TextTrimming = TextTrimming.CharacterEllipsis;
            }));
            grid.HorizontalAlignment = HorizontalAlignment.Stretch;
        });
        it.StaysOpenOnClick = true;
    });
    
    
    // Detach from view-model of log chart.
    void DetachFromLogChart(LogChartViewModel viewModel)
    {
        // remove handlers
        viewModel.PropertyChanged -= this.OnLogChartViewModelPropertyChanged;
        this.DetachFromRawLogChartSeries(false);
        this.DetachFromRawVisibleLogChartSeries(true);
        (viewModel.SeriesSources as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnLogChartSeriesSourcesChanged);
        (viewModel.VisibleSeriesSources as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnVisibleLogChartSeriesSourcesChanged);
        
        // update log chart
        this.InvalidateLogChartSeriesColors();
        if (this.visibleLogChartSeriesMenu is not null)
        {
            this.visibleLogChartSeriesMenu.Close();
            this.visibleLogChartSeriesMenu = null;
        }
    }
    
    
    // Detach from current raw series of log chart.
    void DetachFromRawLogChartSeries(bool updateSeries)
    {
        if (this.attachedRawLogChartSeries is not null)
            this.attachedRawLogChartSeries.CollectionChanged -= this.OnRawLogChartSeriesChanged;
        this.attachedRawLogChartSeries = null;
        if (updateSeries)
            this.logChartSeries.Clear();
    }
    
    
    // Detach from current visible raw series of log chart.
    void DetachFromRawVisibleLogChartSeries(bool updateSeries)
    {
        if (this.attachedRawVisibleLogChartSeries is not null)
            this.attachedRawVisibleLogChartSeries.CollectionChanged -= this.OnRawVisibleLogChartSeriesChanged;
        this.attachedRawVisibleLogChartSeries = null;
        if (updateSeries)
            this.logChartSeries.Clear();
    }
    
    
    // Get proper name of series to be displayed.
    string GetLogChartSeriesDisplayName(DisplayableLogChartSeriesSource source)
    {
        var seriesNameBuffer = new StringBuilder(source.PropertyDisplayName);
        source.SecondaryPropertyDisplayName.Let(it =>
        {
            if (string.IsNullOrEmpty(it))
                return;
            seriesNameBuffer.Append(" - ");
            seriesNameBuffer.Append(it);
        });
        source.Quantifier.Let(it =>
        {
            if (string.IsNullOrEmpty(it))
                return;
            seriesNameBuffer.Append(" (");
            seriesNameBuffer.Append(it);
            seriesNameBuffer.Append(')');
        });
        return seriesNameBuffer.ToString();
    }
    
    
    // Get X label of tool tip of log chart.
    string GetLogChartXToolTipLabel<TVisual>(ChartPoint<DisplayableLogChartSeriesValue?, TVisual, LabelGeometry> point) =>
        this.GetLogChartXToolTipLabel(point.Model);
    string GetLogChartXToolTipLabel(ChartPoint point, bool isInverted)
    {
        if (point.Context.Series is not ICartesianSeries cartesianSeries
            || cartesianSeries.Tag is not DisplayableLogChartSeries series)
        {
            return "";
        }
        var index = (int)(point.Coordinate.SecondaryValue / this.logChartXCoordinateScaling + 0.5);
        if (isInverted)
            index = series.Values.Count - index - 1;
        if (index < 0 || index >= series.Values.Count)
            return "";
        return this.GetLogChartXToolTipLabel(series.Values[index]);
    }
    string GetLogChartXToolTipLabel(DisplayableLogChartSeriesValue? value)
        => this.GetLogChartXToolTipLabel(value?.Log, false, false);
    string GetLogChartXToolTipLabel(DisplayableLog? log, bool useSimpleFormat, bool allowEmpty)
    {
        var label = log?.Let(log =>
        {
            if (!useSimpleFormat)
            {
                return (log.TimestampString
                        ?? log.TimeSpanString
                        ?? log.BeginningTimestampString
                        ?? log.BeginningTimeSpanString
                        ?? log.EndingTimestampString
                        ?? log.EndingTimeSpanString
                        ?? log.ReadTimeString)?.ToString();
            }
            return log.Timestamp?.Let(it => it.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                   ?? log.TimeSpan?.Let(it => it.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture))
                   ?? log.BeginningTimestamp?.Let(it => it.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                   ?? log.BeginningTimeSpan?.Let(it => it.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture))
                   ?? log.EndingTimestamp?.Let(it => it.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                   ?? log.EndingTimeSpan?.Let(it => it.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture))
                   ?? log.ReadTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        });
        if (!string.IsNullOrEmpty(label))
            return label;
        return allowEmpty ? "" : this.Application.GetStringNonNull("SessionView.LogChart.NoLogTimestamp");
    }
    
    
    // Get Y label of tool tip of log chart.
    string GetLogChartYToolTipLabel<TVisual>(DisplayableLogChartSeries series, ChartPoint<DisplayableLogChartSeriesValue?, TVisual, LabelGeometry> point, bool includeSeriesName) =>
        this.GetLogChartYToolTipLabel(series, (ChartPoint)point, includeSeriesName);
    string GetLogChartYToolTipLabel(DisplayableLogChartSeries series, ChartPoint point, bool includeSeriesName)
    {
        // get state
        var source = series.Source;
        var viewModel = (this.DataContext as Session)?.LogChart;
        var chartType = viewModel?.ChartType ?? LogChartType.None;
        var buffer = new StringBuilder();
        
        // source name
        if (includeSeriesName && source is not null)
        {
            buffer.Append(this.GetLogChartSeriesDisplayName(source));
            buffer.Append(": ");
        }
        
        // value
        if (chartType.IsDirectNumberValueSeriesType())
            buffer.Append(viewModel?.GetYAxisLabel(point.Coordinate.PrimaryValue) ?? point.Coordinate.PrimaryValue.ToString(this.Application.CultureInfo));
        else if (chartType.IsStatisticalSeriesType())
        {
            var index = (int)(point.Coordinate.SecondaryValue / this.logChartXCoordinateScaling + 0.5);
            if (index >= 0 && index < series.Values.Count)
                buffer.Append(series.Values[index]?.Label);
        }
        switch (viewModel?.ChartValueGranularity ?? LogChartValueGranularity.Default)
        {
            case LogChartValueGranularity.Byte:
            case LogChartValueGranularity.Kilobytes:
            case LogChartValueGranularity.Megabytes:
            case LogChartValueGranularity.Gigabytes:
                break;
            default:
                source?.Quantifier.Let(it =>
                {
                    if (string.IsNullOrEmpty(it))
                        return;
                    buffer.Append(' ');
                    buffer.Append(it);
                });
                break;
        }
        
        // statistical value
        if (chartType.IsStatisticalSeriesType())
        {
            buffer.Append(" (");
            buffer.Append(point.Coordinate.PrimaryValue.ToString(this.Application.CultureInfo));
            buffer.Append(')');
        }
        
        // complete
        return buffer.ToString();
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
    public Paint LogChartLegendBackgroundPaint => this.GetValue(LogChartLegendBackgroundPaintProperty);
    
    
    /// <summary>
    /// Get foreground paint for legend of log chart.
    /// </summary>
    public Paint LogChartLegendForegroundPaint => this.GetValue(LogChartLegendForegroundPaintProperty);


    /// <summary>
    /// Get background paint for tool tip of log chart.
    /// </summary>
    public Paint LogChartToolTipBackgroundPaint => this.GetValue(LogChartToolTipBackgroundPaintProperty);
    
    
    /// <summary>
    /// Get border paint for tool tip of log chart.
    /// </summary>
    public Paint LogChartToolTipBorderPaint => this.GetValue(LogChartToolTipBorderPaintProperty);
    
    
    /// <summary>
    /// Get border thickness for tool tip of log chart.
    /// </summary>
    public double LogChartToolTipBorderThickness => this.GetValue(LogChartToolTipBorderThicknessProperty);


    /// <summary>
    /// Get foreground paint for tool tip of log chart.
    /// </summary>
    public Paint LogChartToolTipForegroundPaint => this.GetValue(LogChartToolTipForegroundPaintProperty);


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
            var index = (int)(point.Coordinate.SecondaryValue / this.logChartXCoordinateScaling + 0.5);
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
        if (this.logChartPointerDownPosition.HasValue && this.logChartPointerDownData.IsNotEmpty())
        {
            var downPosition = this.logChartPointerDownPosition.Value;
            var position = e.GetPosition(this.logChart);
            var diffX = (position.X - downPosition.X);
            var diffY = (position.Y - downPosition.Y);
            var distance = Math.Sqrt(diffX * diffX + diffY * diffY);
            if (distance >= PointerDistanceToDropClickEvent)
            {
                this.logChartPointerDownData = [];
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
            this.isPointerPressedOnLogChart = true;
            this.logChartPointerDownPosition = point.Position;
            this.logChartPointerDownWatch.Restart();
        }
        this.UpdateLogChartCursor();
        this.UpdateLogChartToolTip();
    }
    
    
    // Called when pointer wheel changed on log chart.
    void OnLogChartPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var steps = e.Delta.Y;
        if (Math.Abs(steps) < 0.01)
            return;
        var position = e.GetPosition(this.logChart).Let(it => new LvcPoint(it.X, it.Y));
        if (this.logChart.Series.FirstOrDefault() is not { } series)
            return;
        if (series.FindHitPoints(this.logChart.CoreChart, position, FindingStrategy.CompareOnlyXTakeClosest, FindPointFor.HoverEvent).FirstOrDefault() is not { } point)
            return;
        this.ZoomLogChartWithSteps(point, steps);
    }
    
    
    // Called when pointer released on log chart.
    void OnLogChartPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            // reset state
            this.isPointerPressedOnLogChart = false;
            
            // update cursor and tool tip
            this.UpdateLogChartCursor();
            this.UpdateLogChartToolTip();
            
            // double click
            if (this.isLogChartDoubleTapped)
            {
                this.isLogChartDoubleTapped = false;
                this.logChartPointerDownPosition = null;
                this.logChartPointerDownWatch.Reset();
                if (this.GetValue(IsLogChartHorizontallyZoomedProperty))
                    this.SynchronizationContext.Post(this.ResetLogChartZoom);
                else
                {
                    this.SynchronizationContext.Post(() =>
                    {
                        if (!this.GetValue(IsLogChartHorizontallyZoomedProperty) && !this.isPointerPressedOnLogChart)
                        {
                            var position = e.GetPosition(this.logChart).Let(it => new LvcPoint(it.X, it.Y));
                            if (this.logChart.Series.FirstOrDefault() is { } series
                                && series.FindHitPoints(this.logChart.CoreChart, position, FindingStrategy.CompareOnlyXTakeClosest, FindPointFor.PointerDownEvent).FirstOrDefault() is { } point)
                            {
                                this.ZoomLogChartTo(point, LogChartXRangeWhenDoubleClick);
                            }
                        }
                    });
                }
                return;
            }
            
            // single click
            if (this.logChartPointerDownData.IsNotEmpty())
            {
                var data = this.logChartPointerDownData;
                this.logChartPointerDownData = [];
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
                    var minLimit = this.logChartXAxis.MinLimit;
                    var maxLimit = this.logChartXAxis.MaxLimit;
                    this.logChartXAxisLimitToKeep = (minLimit, maxLimit); // [Workaround] Prevent changing limit by click
                }
                this.logChartPointerDownWatch.Reset();
            }
        }
    }
    
    
    // Called when magnify gesture on touchpad detected.
    void OnLogChartPointerTouchPadGestureMagnify(object? sender, PointerDeltaEventArgs e)
    {
        var delta = e.Delta;
        var steps = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) * 2.0;
        if (Math.Abs(steps) < 0.01)
            return;
        var position = e.GetPosition(this.logChart).Let(it => new LvcPoint(it.X, it.Y));
        if (this.logChart.Series.FirstOrDefault() is not { } series)
            return;
        if (series.FindHitPoints(this.logChart.CoreChart, position, FindingStrategy.CompareOnlyXTakeClosest, FindPointFor.HoverEvent).FirstOrDefault() is not { } point)
            return;
        this.ZoomLogChartWithSteps(point, Math.Sign(delta.X) * steps);
    }
    
    
    // Called when property of log chart has been changed.
    void OnLogChartPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CartesianChart.ZoomModeProperty)
        {
            this.UpdateLogChartCursor();
            this.UpdateLogChartToolTip();
        }
    }
    
    
    // Called when list of source of series of log chart changed.
    void OnLogChartSeriesSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.visibleLogChartSeriesMenu?.ItemsSource is not ObservableList<object> menuItems)
            return;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<DisplayableLogChartSeriesSource>().Let(newSources =>
                {
                    var startIndex = e.NewStartingIndex;
                    for (int i = 0, count = newSources.Count; i < count; ++i)
                        menuItems.Insert(startIndex + i, this.CreateVisibleLogChartSeriesMenuItem(newSources[i]));
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogChartSeriesSource>().Let(oldSources =>
                    menuItems.RemoveRange(e.OldStartingIndex, oldSources.Count));
                break;
            case NotifyCollectionChangedAction.Reset:
                if (menuItems.Count > 2)
                    menuItems.RemoveRange(0, menuItems.Count - 2);
                (this.DataContext as Session)?.LogChart.SeriesSources.Let(sources =>
                {
                    for (int i = 0, count = sources.Count; i < count; ++i)
                        menuItems.Insert(i, this.CreateVisibleLogChartSeriesMenuItem(sources[i]));
                });
                break;
            default:
                throw new NotSupportedException();
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
                this.areLogChartAxesReady = false;
                this.UpdateLogChartSeries();
                break;
            case nameof(LogChartViewModel.ChartValueGranularity):
            case nameof(LogChartViewModel.IsChartDefined):
                this.UpdateLogChartSeries();
                break;
            case nameof(LogChartViewModel.IsMaxTotalSeriesValueCountReached):
                if (viewModel.IsMaxTotalSeriesValueCountReached)
                    _ = this.PromptForMaxLogChartSeriesValueCountReached();
                break;
            case nameof(LogChartViewModel.IsPanelVisible):
                this.UpdateLogChartPanelVisibility();
                this.ShowLogChartTutorial();
                break;
            case nameof(LogChartViewModel.IsXAxisInverted):
            case nameof(LogChartViewModel.IsYAxisInverted):
                this.areLogChartAxesReady = false;
                this.UpdateLogChartAxes();
                break;
            case nameof(LogChartViewModel.MaxVisibleSeriesValue):
            case nameof(LogChartViewModel.MinVisibleSeriesValue):
                this.updateLogChartYAxisLimitAction.Schedule();
                break;
            case nameof(LogChartViewModel.MaxSeriesValueCount):
                this.updateLogChartXAxisLimitAction.Schedule();
                if (viewModel.MaxSeriesValueCount == 0)
                    this.ResetLogChartZoom();
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
                this.AttachToRawLogChartSeries(viewModel.Series, true);
                break;
            case nameof(LogChartViewModel.VisibleSeries):
                this.AttachToRawLogChartSeries(viewModel.VisibleSeries, true);
                break;
            case nameof(LogChartViewModel.XAxisType):
                this.areLogChartAxesReady = false;
                this.UpdateLogChartAxes();
                break;
            case nameof(LogChartViewModel.YAxisName):
                this.areLogChartAxesReady = false;
                this.UpdateLogChartAxes();
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
                // add series
                e.NewItems!.Cast<DisplayableLogChartSeries>().Let(series =>
                {
                    var startIndex = e.NewStartingIndex;
                    for (int i = 0, count = series.Count; i < count; ++i)
                    {
                        var lvcSeries = this.CreateLogChartSeries(viewModel, series[i]);
                        lvcSeries.IsVisible = viewModel.VisibleSeries.Contains(series[i]);
                        this.logChartSeries.Insert(startIndex + i, lvcSeries);
                    }
                });
                
                // update axes
                this.logChartXAxis.UnitWidth = this.logChartXCoordinateScaling;
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
    
    
    // Called when collection of visible series changed by view-model.
    void OnRawVisibleLogChartSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.DataContext is not Session session)
            return;
        var lvcSeriesCount = this.logChartSeries.Count;
        if (lvcSeriesCount == 0)
            return;
        var viewModel = session.LogChart;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<DisplayableLogChartSeries>().Let(newSeries =>
                {
                    var series = viewModel.Series;
                    for (var i = Math.Min(series.Count, lvcSeriesCount) - 1; i >= 0; --i)
                    {
                        if (newSeries.Contains(series[i]))
                            this.logChartSeries[i].IsVisible = true;
                    }
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogChartSeries>().Let(oldSeries =>
                {
                    var series = viewModel.Series;
                    for (var i = Math.Min(series.Count, lvcSeriesCount) - 1; i >= 0; --i)
                    {
                        if (oldSeries.Contains(series[i]))
                            this.logChartSeries[i].IsVisible = false;
                    }
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                this.UpdateLogChartSeries();
                break;
            default:
                throw new NotSupportedException();
        }
    }


    // Called when list of visible source of series of log chart changed.
    void OnVisibleLogChartSeriesSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.visibleLogChartSeriesMenu?.ItemsSource is not IList<object> menuItems)
            return;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<DisplayableLogChartSeriesSource>().Let(newSources =>
                {
                    foreach (var item in menuItems)
                    {
                        if (item is MenuItem menuItem 
                            && menuItem.DataContext is DisplayableLogChartSeriesSource source
                            && newSources.Contains(source)
                            && menuItem.Icon is Control icon)
                        {
                            icon.IsVisible = true;
                        }
                    }
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<DisplayableLogChartSeriesSource>().Let(oldSources =>
                {
                    foreach (var item in menuItems)
                    {
                        if (item is MenuItem menuItem 
                            && menuItem.DataContext is DisplayableLogChartSeriesSource source
                            && oldSources.Contains(source)
                            && menuItem.Icon is Control icon)
                        {
                            icon.IsVisible = false;
                        }
                    }
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                (this.DataContext as Session)?.LogChart.VisibleSeriesSources.Let(sources =>
                {
                    foreach (var item in menuItems)
                    {
                        if (item is MenuItem menuItem 
                            && menuItem.DataContext is DisplayableLogChartSeriesSource source
                            && menuItem.Icon is Control icon)
                        {
                            icon.IsVisible = sources.Contains(source);
                        }
                    }
                });
                break;
            default:
                throw new NotSupportedException();
        }
    }


    // Show message dialog to notify user that the total number of values reaches the limitation.
    async Task PromptForMaxLogChartSeriesValueCountReached()
    {
        if (!this.PersistentState.GetValueOrDefault(PromptWhenMaxTotalLogSeriesValueCountReachedKey))
            return;
        if (this.DataContext is not Session session || !session.IsProVersionActivated)
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
    
    
    // Scroll to start of end of log chart.
    void ScrollToEdgeOfLogChart(bool scrollToStart)
    {
        if (!this.GetValue(IsLogChartHorizontallyZoomedProperty))
            return;
        var dataBounds = this.logChartXAxis.DataBounds;
        var minCoordinate = dataBounds.Min;
        var maxCoordinate = dataBounds.Max;
        var minLimit = this.logChartXAxis.MinLimit ?? minCoordinate;
        var maxLimit = this.logChartXAxis.MaxLimit ?? maxCoordinate;
        var range = (maxLimit - minLimit);
        if (this.logChartXAxis.IsInverted)
            scrollToStart = !scrollToStart;
        if (scrollToStart)
        {
            this.logChartXAxis.MinLimit = minCoordinate;
            this.logChartXAxis.MaxLimit = minCoordinate + range;
        }
        else
        {
            this.logChartXAxis.MinLimit = maxCoordinate - range;
            this.logChartXAxis.MaxLimit = maxCoordinate;
        }
        this.updateLogChartXAxisLimitAction.Execute();
    }


    /// <summary>
    /// Horizontally scroll to end of log chart.
    /// </summary>
    public void ScrollToEndOfLogChart() =>
        this.ScrollToEdgeOfLogChart(false);


    /// <summary>
    /// Horizontally scroll to start of log chart.
    /// </summary>
    public void ScrollToStartOfLogChart() =>
        this.ScrollToEdgeOfLogChart(true);


    // Select animation speed of log chart series.
    TimeSpan SelectLogChartSeriesAnimationSpeed()
    {
        if (this.DataContext is not Session session)
            return default;
        return this.SelectLogChartSeriesAnimationSpeed(session.LogChart);
    }
    TimeSpan SelectLogChartSeriesAnimationSpeed(LogChartViewModel viewModel) =>
        this.startLogChartAnimationsAction.IsScheduled
            ? TimeSpan.Zero
            : this.Application.FindResourceOrDefault("TimeSpan/Animation.Fast", TimeSpan.FromMilliseconds(200));
    
    
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


    // Select proper SKTypeface for displaying.
    SKTypeface SelectSKTypeface()
    {
        var fontManager = SKFontManager.Default;
        var cultureName = this.Application.CultureInfo.Name;
        if (cultureName.StartsWith("zh"))
        {
            switch (this.Application.ChineseVariant)
            {
                case ChineseVariant.Default:
                    NotoSansSCSKTypeFace ??= BuiltInFonts.OpenStream(nameof(BuiltInFonts.NotoSansSC)).Use(stream => fontManager.CreateTypeface(stream));
                    return NotoSansSCSKTypeFace;
                case ChineseVariant.Taiwan:
                    NotoSansTCSKTypeFace ??= BuiltInFonts.OpenStream(nameof(BuiltInFonts.NotoSansTC)).Use(stream => fontManager.CreateTypeface(stream));
                    return NotoSansTCSKTypeFace;
                default:
                    throw new NotImplementedException();
            }
        }
        InterSKTypeFace ??= BuiltInFonts.OpenStream(nameof(BuiltInFonts.Inter)).Use(stream => fontManager.CreateTypeface(stream));
        return InterSKTypeFace;
    }


    /// <summary>
    /// Let user select visible series of log chart.
    /// </summary>
    public void SelectVisibleLogChartSeries()
    {
        // check state
        if (this.DataContext is not Session session)
            return;
        
        // setup button and menu
        var viewModel = session.LogChart;
        this.visibleLogChartSeriesButton ??= this.Get<ToggleButton>(nameof(visibleLogChartSeriesButton));
        this.visibleLogChartSeriesMenu ??= new ContextMenu().Also(menu =>
        {
            // setup items
            var menuItems = new ObservableList<object>();
            foreach (var source in viewModel.SeriesSources)
                menuItems.Add(this.CreateVisibleLogChartSeriesMenuItem(source));
            menuItems.Add(new Separator());
            menuItems.Add(new MenuItem().Also(it =>
            {
                it.Click += (_, e) =>
                {
                    e.Handled = true;
                    (this.DataContext as Session)?.LogChart.ResetVisibleSeriesSourcesCommand.TryExecute();
                };
                it.BindToResource(HeaderedSelectingItemsControl.HeaderProperty, this, "String/Common.Reset");
                it.StaysOpenOnClick = true;
            }));
            menu.ItemsSource = menuItems;
            
            // setup menu
            menu.Closed += (_, _) =>
            {
                if (this.visibleLogChartSeriesMenu == menu)
                    this.visibleLogChartSeriesButton.IsChecked = false;
            };
            menu.Opened += (_, _) =>
            {
                if (this.visibleLogChartSeriesMenu == menu)
                    this.visibleLogChartSeriesButton.IsChecked = true;
            };
            menu.Placement = PlacementMode.Top;
        });
        
        // open menu
        this.visibleLogChartSeriesMenu.Open(this.visibleLogChartSeriesButton);
    }
    
    
    // Show tutorial of log chart if needed.
    void ShowLogChartTutorial()
    {
        // check state
        if (this.PersistentState.GetValueOrDefault(IsLogChartTutorialShownKey))
            return;
        if (this.attachedWindow is not MainWindow window || window.CurrentTutorial != null || !window.IsActive)
            return;
        if (this.DataContext is not Session session || !session.IsActivated || !session.IsProVersionActivated)
            return;
        var viewModel = session.LogChart;
        if (!viewModel.IsPanelVisible || !viewModel.IsChartDefined)
            return;

        // show tutorial
        window.ShowTutorial(new Tutorial().Also(it =>
        {
            it.Anchor = this.logChart;
            it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.LogChart"));
            it.Dismissed += (_, _) => 
                this.PersistentState.SetValue<bool>(IsLogChartTutorialShownKey, true);
            it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
            it.IsSkippingAllTutorialsAllowed = false;
        }));
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
                    it.VerticalAlignment = VerticalAlignment.Center;
                });
                var currentTypeTextBlock = new Avalonia.Controls.TextBlock().Also(it =>
                {
                    it.Opacity = app.FindResourceOrDefault<double>("Double/SessionView.LogChartTypeMenu.CurrentLogChartType.Opacity");
                    it.Bind(Avalonia.Controls.TextBlock.TextProperty, app.GetObservableString("SessionView.LogChartTypeMenu.CurrentLogChartType"));
                    it.TextTrimming = TextTrimming.CharacterEllipsis;
                    it.VerticalAlignment = VerticalAlignment.Center;
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
    
    
    // Update all animations of log chart.
    void UpdateLogChartAnimations()
    {
        var animationSpeed = this.SelectLogChartSeriesAnimationSpeed();
        foreach (var series in this.logChartSeries)
            series.AnimationsSpeed = animationSpeed;
    }
    
    
    // Update axes of log chart.
    void UpdateLogChartAxes()
    {
        if (this.areLogChartAxesReady)
            return;
        var app = this.Application;
        var axisFontSize = app.FindResourceOrDefault("Double/SessionView.LogChart.Axis.FontSize", 10.0);
        var axisWidth = app.FindResourceOrDefault("Double/SessionView.LogChart.Axis.Width", 2.0);
        var xAxisPadding = app.FindResourceOrDefault("Thickness/SessionView.LogChart.Axis.Padding.X", default(Thickness)).Let(t => new Padding(t.Left, t.Top, t.Right, t.Bottom));
        var yAxisPadding = app.FindResourceOrDefault("Thickness/SessionView.LogChart.Axis.Padding.Y", default(Thickness)).Let(t => new Padding(t.Left, t.Top, t.Right, t.Bottom));
        var textBrush = app.FindResourceOrDefault<ISolidColorBrush>("TextControlForeground", Brushes.Black);
        var crosshairBrush = app.FindResourceOrDefault<ISolidColorBrush>("Brush/SessionView.LogChart.Axis.Crosshair", Brushes.Black);
        var crosshairWidth = app.FindResourceOrDefault("Double/SessionView.LogChart.Axis.Crosshair.Width", 1.0);
        var separatorBrush = app.FindResourceOrDefault<ISolidColorBrush>("Brush/SessionView.LogChart.Axis.Separator", Brushes.Black);
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
            var viewModel = (this.DataContext as Session)?.LogChart;
            var chartType = viewModel?.ChartType ?? LogChartType.None;
            var axisType = viewModel?.XAxisType ?? LogChartXAxisType.None;
            axis.IsInverted = viewModel?.IsXAxisInverted ?? false;
            if (axisType != LogChartXAxisType.None && chartType.IsDirectNumberValueSeriesType())
            {
                var textPaint = textBrush.Let(brush =>
                {
                    var color = brush.Color;
                    return new SolidColorPaint(new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5)));
                });
                axis.Labeler = value =>
                {
                    if (this.DataContext is Session session)
                    {
                        var doubleIndex = (value / this.logChartXCoordinateScaling);
                        var intIndex = (int)(doubleIndex + 0.5);
                        var logs = session.Logs;
                        if (axis.IsInverted)
                        {
                            var diff = doubleIndex - intIndex;
                            intIndex = (logs.Count - intIndex - 1);
                            doubleIndex = intIndex - diff;
                        }
                        if (Math.Abs(intIndex - doubleIndex) < 0.001 && intIndex >= 0 && intIndex < logs.Count)
                            return this.GetLogChartXToolTipLabel(logs[intIndex], axisType == LogChartXAxisType.SimpleTimestamp, true);
                        return "";
                    }
                    return value.ToString(this.Application.CultureInfo);
                };
                axis.LabelsPaint = textPaint;
                axis.Padding = xAxisPadding;
                axis.UnitWidth = this.logChartXCoordinateScaling;
            }
            else
                axis.LabelsPaint = null;
            axis.TextSize = (float)axisFontSize;
            axis.ZeroPaint = null;
        });
        this.logChartYAxis.Let(axis =>
        {
            var viewModel = (this.DataContext as Session)?.LogChart;
            var textPaint = textBrush.Let(brush =>
            {
                var color = brush.Color;
                return new SolidColorPaint(new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5)));
            });
            axis.IsInverted = viewModel?.IsYAxisInverted ?? false;
            axis.Labeler = value =>
            {
                if (this.DataContext is Session session)
                    return session.LogChart.GetYAxisLabel(value);
                return value.ToString(this.Application.CultureInfo);
            };
            axis.LabelsPaint = textPaint;
            axis.Name = viewModel?.YAxisName ?? "";
            axis.NamePaint = string.IsNullOrWhiteSpace(axis.Name)
                ? null
                : new SolidColorPaint(textPaint.Color)
                {
                    SKTypeface = SKTypeface.FromFamilyName(this.SelectSKTypeface().FamilyName, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                };
            axis.NameTextSize = (float)this.Application.FindResourceOrDefault("Double/SessionView.LogChart.Axis.Name.FontSize", 18.0);
            axis.Padding = yAxisPadding;
            axis.SeparatorsPaint = separatorBrush.Let(brush =>
            {
                var color = brush.Color;
                return new SolidColorPaint
                {
                    Color = new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5)),
                    StrokeThickness = (float)this.FindResourceOrDefault("Double/DisplayableLogChartSeriesGenerator.Axis.Separator.Width", 1.0),
                    PathEffect = new DashEffect([ 3, 3 ]),
                };
            });
            axis.TextSize = (float)axisFontSize;
            axis.ZeroPaint = new SolidColorPaint(textPaint.Color, (float)axisWidth);
        });
        this.UpdateLogChartToolTip();
        this.areLogChartAxesReady = true;
    }
    
    
    // Update cursor of log chart.
    void UpdateLogChartCursor()
    {
        if (this.logChart.ZoomMode == ZoomAndPanMode.None || !this.isPointerPressedOnLogChart)
        {
            this.logChartHandCursorValueToken = this.logChartHandCursorValueToken.DisposeAndReturnNull();
            return;
        }
        if (this.logChartHandCursorValueToken is not null)
            return;
        this.logChartHandCursor ??= this.LoadCursor("Image/Cursor.Hand");
        this.logChartHandCursorValueToken = this.logChart.SetValue(CursorProperty, this.logChartHandCursor, BindingPriority.Template);
    }


    // Update paints for log chart.
    void UpdateLogChartPaints()
    {
        this.Application.FindResourceOrDefault<ISolidColorBrush?>("ToolTipBackground")?.Let(brush =>
        {
            var color = brush.Color;
            var skColor = new SKColor(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5));
            this.SetValue(LogChartLegendBackgroundPaintProperty, new SolidColorPaint(skColor) { ZIndex = LogChartLegendZIndex });
            this.SetValue(LogChartToolTipBackgroundPaintProperty, new SolidColorPaint(skColor) { ZIndex = LogChartToolTipZIndex});
        });
        this.Application.FindResourceOrDefault<ISolidColorBrush?>("ToolTipBorderBrush")?.Let(brush =>
        {
            var color = brush.Color;
            var skColor = new SKColor(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5));
            this.SetValue(LogChartToolTipBorderPaintProperty, new SolidColorPaint(skColor) { ZIndex = LogChartToolTipZIndex});
        });
        this.Application.FindResourceOrDefault<Thickness>("ToolTipBorderThemeThickness").Let(thickness =>
        {
            this.SetValue(LogChartToolTipBorderThicknessProperty, (thickness.Left + thickness.Right) * 0.5);
        });
        this.Application.FindResourceOrDefault<ISolidColorBrush?>("ToolTipForeground")?.Let(brush =>
        {
            var typeface = this.SelectSKTypeface();
            var color = brush.Color;
            var skColor = new SKColor(color.R, color.G, color.B, (byte)(color.A * brush.Opacity + 0.5));
            this.SetValue(LogChartLegendForegroundPaintProperty, new SolidColorPaint(skColor) { SKTypeface = typeface, ZIndex = LogChartLegendZIndex + 1 });
            this.SetValue(LogChartToolTipForegroundPaintProperty, new SolidColorPaint(skColor) { SKTypeface = typeface, ZIndex = LogChartToolTipZIndex + 1 });
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
        {
            var lvcSeries = this.CreateLogChartSeries(viewModel, series);
            lvcSeries.IsVisible = viewModel.VisibleSeries.Contains(series);
            this.logChartSeries.Add(lvcSeries);
        }
        ((int)this.logChart.AnimationsSpeed.TotalMilliseconds).Let(it => // [Workaround] Prevent animation interrupted unexpectedly
        {
            if (it > 100)
                this.SynchronizationContext.PostDelayed(() => this.logChart.CoreChart.Update(), 100);
            this.SynchronizationContext.PostDelayed(() => this.logChart.CoreChart.Update(), it);
        });
        
        // update axes
        this.logChartXAxis.UnitWidth = this.logChartXCoordinateScaling;
        
        // update zoom mode
        this.UpdateLogChartZoomMode();
    }
    
    
    // Update visibility of log chart panel.
    void UpdateLogChartPanelVisibility()
    { }
    
    
    // Update tooltip of log chart.
    void UpdateLogChartToolTip()
    {
        if (this.logChart.ZoomMode != ZoomAndPanMode.None && this.isPointerPressedOnLogChart)
        {
            this.logChart.Tooltip = null;
            return;
        }
        if (this.logChartToolTip is null || this.logChartToolTip.IsXAxisInverted != this.logChartXAxis.IsInverted)
            this.logChartToolTip = new(this, this.logChartXAxis.IsInverted);
        this.logChart.Tooltip = this.logChartToolTip;
    }
    
    
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
        var dataBounds = axis.DataBounds;
        var minCoordinate = dataBounds.Min;
        var maxCoordinate = dataBounds.Max;
        var minLimit = axis.MinLimit ?? double.NaN;
        var maxLimit = axis.MaxLimit ?? double.NaN;
        if (this.logChartXAxisLimitToKeep.HasValue)
        {
            var limit = this.logChartXAxisLimitToKeep.Value;
            this.logChartXAxisLimitToKeep = null;
            if (limit.Item1.HasValue)
            {
                if (double.IsNaN(minLimit) || Math.Abs(minLimit - limit.Item1.Value) >= 0.001)
                    axis.MinLimit = limit.Item1.Value;
            }
            else if (double.IsFinite(minLimit))
                axis.MinLimit = null;
            if (limit.Item2.HasValue)
            {
                if (double.IsNaN(maxLimit) || Math.Abs(maxLimit - limit.Item2.Value) >= 0.001)
                    axis.MaxLimit = limit.Item2.Value;
            }
            else if (double.IsFinite(maxLimit))
                axis.MaxLimit = null;
            minLimit = limit.Item1 ?? double.NaN;
            maxLimit = limit.Item2 ?? double.NaN;
        }
        if (!double.IsFinite(minLimit) && !double.IsFinite(maxLimit))
        {
            this.SetValue(IsLogChartHorizontallyZoomedProperty, false);
            return;
        }
        if ((maxCoordinate - minCoordinate) <= LogChartXAxisMinRange)
        {
            axis.MinLimit = null;
            axis.MaxLimit = null;
            this.SetValue(IsLogChartHorizontallyZoomedProperty, false);
            return;
        }
        var isSnappedToEdge = false;
        if (!this.isPointerPressedOnLogChart)
        {
            if (minLimit < minCoordinate + 0.5)
            {
                var range = maxLimit - minLimit;
                minLimit = minCoordinate;
                maxLimit = minCoordinate + range;
                if (maxLimit > maxCoordinate - 0.5)
                    maxLimit = maxCoordinate;
                axis.MinLimit = minLimit;
                axis.MaxLimit = maxLimit;
                isSnappedToEdge = true;
            }
            else if (maxLimit > maxCoordinate - 0.5)
            {
                var range = maxLimit - minLimit;
                maxLimit = maxCoordinate;
                minLimit = maxCoordinate - range;
                if (minLimit < minCoordinate + 0.5)
                    minLimit = minCoordinate;
                axis.MinLimit = minLimit;
                axis.MaxLimit = maxLimit;
                isSnappedToEdge = true;
            }
        }
        if (!isSnappedToEdge && (maxLimit - minLimit) < LogChartXAxisMinRange)
        {
            var center = (minLimit + maxLimit) * 0.5;
            minLimit = center - (LogChartXAxisMinRange * 0.5);
            maxLimit = center + (LogChartXAxisMinRange * 0.5);
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
        var minLimit = viewModel.MinVisibleSeriesValue?.Value ?? double.NaN;
        var maxLimit = viewModel.MaxVisibleSeriesValue?.Value ?? double.NaN;
        if (double.IsFinite(minLimit) && double.IsFinite(maxLimit))
        {
            if (minLimit >= 0)
            {
                var range = maxLimit;
                var reserved = Math.Max(0.25, range * LogChartYAxisMinMaxReservedRatio);
                this.logChartYAxis.MinLimit = -reserved;
                this.logChartYAxis.MaxLimit = maxLimit < (double.MaxValue - 1 - reserved)
                    ? maxLimit + reserved
                    : double.MaxValue - 1;
            }
            else if (maxLimit <= 0)
            {
                var range = -minLimit;
                var reserved = Math.Max(0.25, range * LogChartYAxisMinMaxReservedRatio);
                this.logChartYAxis.MinLimit = minLimit > (double.MinValue + 1 + reserved)
                    ? minLimit - reserved
                    : double.MinValue + 1;
                this.logChartYAxis.MaxLimit = reserved;
            }
            else
            {
                var range = (maxLimit - minLimit);
                var reserved = Math.Max(0.25, range * LogChartYAxisMinMaxReservedRatio);
                this.logChartYAxis.MinLimit = minLimit > (double.MinValue + 1 + reserved)
                    ? minLimit - reserved
                    : double.MinValue + 1;
                this.logChartYAxis.MaxLimit = maxLimit < (double.MaxValue - 1 - reserved)
                    ? maxLimit + reserved
                    : double.MaxValue - 1;
            }
            return;
        }
        this.logChartYAxis.MinLimit = null;
        this.logChartYAxis.MaxLimit = null;
    }
    
    
    // Update zoom mode of log chart based on current state.
    void UpdateLogChartZoomMode()
    {
        this.logChart.ZoomMode = this.GetValue(IsLogChartHorizontallyZoomedProperty)
            ? ZoomAndPanMode.X 
            : ZoomAndPanMode.None;
    }
    
    
    // Zoom log chart to given range.
    void ZoomLogChartTo(ChartPoint anchor, double range)
    {
        if (!double.IsFinite(range))
            return;
        var centerCoordinate = anchor.Coordinate.SecondaryValue;
        var dataBounds = this.logChartXAxis.DataBounds;
        var minCoordinate = dataBounds.Min;
        var maxCoordinate = dataBounds.Max;
        var minLimit = this.logChartXAxis.MinLimit ?? minCoordinate;
        var maxLimit = this.logChartXAxis.MaxLimit ?? maxCoordinate;
        if (centerCoordinate < minLimit || centerCoordinate > maxLimit)
            return;
        var currentRange = (maxLimit - minLimit);
        var maxRange = (maxCoordinate - minCoordinate);
        range = Math.Max(range, LogChartXAxisMinRange);
        if (Math.Abs(range - currentRange) < 0.1)
            return;
        if (range >= maxRange - 0.5)
        {
            this.logChartXAxis.MinLimit = null;
            this.logChartXAxis.MaxLimit = null;
        }
        else
        {
            var startingRatio = (centerCoordinate - minLimit) / currentRange;
            var newMinLimit = centerCoordinate - (range * startingRatio);
            var newMaxLimit = newMinLimit + range;
            if (newMinLimit < minCoordinate)
            {
                newMinLimit = minCoordinate;
                newMaxLimit = minCoordinate + range;
            }
            else if (newMaxLimit > maxCoordinate)
            {
                newMaxLimit = maxCoordinate;
                newMinLimit = maxCoordinate - range;
            }
            this.logChartXAxis.MinLimit = newMinLimit;
            this.logChartXAxis.MaxLimit = newMaxLimit;
        }
        this.logChartXAxisLimitToKeep = null;
        this.updateLogChartXAxisLimitAction.Execute();
    }
    
    
    // Zoom in or out log chart.
    void ZoomLogChartWithSteps(ChartPoint anchor, double steps /* positive to zoom in */)
    {
        if (!double.IsFinite(steps) || Math.Abs(steps) < 0.01)
            return;
        var dataBounds = this.logChartXAxis.DataBounds;
        var minCoordinate = dataBounds.Min;
        var maxCoordinate = dataBounds.Max;
        var minLimit = this.logChartXAxis.MinLimit ?? minCoordinate;
        var maxLimit = this.logChartXAxis.MaxLimit ?? maxCoordinate;
        var currentRange = (maxLimit - minLimit);
        this.ZoomLogChartTo(anchor, currentRange * Math.Pow(0.7, steps));
    }
}