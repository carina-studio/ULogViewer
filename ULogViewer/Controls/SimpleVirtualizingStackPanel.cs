using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls;

public class SimpleVirtualizingStackPanel : VirtualizingPanel
{
    /// <summary>
    /// Define <see cref="ItemHeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ItemHeightProperty = AvaloniaProperty.Register<SimpleVirtualizingStackPanel, double>(nameof(ItemHeight), 20);
    /// <summary>
    /// Maximum duration to realizing containers in single measurement.
    /// </summary>
    public static readonly SettingKey<int> MaxRealizingContainersDuration = new($"{nameof(SimpleVirtualizingStackPanel)}.{nameof(MaxRealizingContainersDuration)}", 66);
    /// <summary>
    /// Minimum interval between realizing containers in two measurements.
    /// </summary>
    public static readonly SettingKey<int> MinRealizingContainersInterval = new($"{nameof(SimpleVirtualizingStackPanel)}.{nameof(MinRealizingContainersInterval)}", 33);
    
    
    // Static fields.
    static readonly AttachedProperty<object?> RecycleKeyProperty = AvaloniaProperty.RegisterAttached<SimpleVirtualizingStackPanel, Control, object?>("RecycleKey");
    static readonly Stopwatch Stopwatch = new Stopwatch().Also(it => it.Start());
    
    
    // Fields.
    IAppSuiteApplication? app;
    int firstRealizedIndex = -1;
    readonly ScheduledAction invalidateMeasureAction;
    int lastRealizedIndex = -1;
    readonly List<Control?> realizedContainers = new();
    Control? realizingContainer;
    int realizingContainerIndex = -1;
    readonly Dictionary<object, Stack<Control>> recycledContainers = new();
    ScrollViewer? scrollViewer;
    IDisposable? scrollViewerOffsetObserverToken;
    IDisposable? scrollViewerViewportObserverToken;
    Window? window;


    // Static initializer.
    static SimpleVirtualizingStackPanel()
    {
        AffectsMeasure<SimpleVirtualizingStackPanel>(ItemHeightProperty);
    }


    /// <summary>
    /// Initialize new <see cref="SimpleVirtualizingStackPanel"/> instance.
    /// </summary>
    public SimpleVirtualizingStackPanel()
    {
        this.invalidateMeasureAction = new(this.InvalidateMeasure);
    }


    // Align given value with physical pixels
    static double AlignToPixels(double value, Screen? screen) =>
        AlignToPixels(value, screen?.Scaling ?? 1.0);
    static double AlignToPixels(double value, double screenScale) =>
        Math.Abs(screenScale - 1) > 0.01 ? (int)(value * screenScale + 0.5) / screenScale : (int)(value + 0.5);


    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (this.firstRealizedIndex < 0)
            return default;
        var screen = this.GetScreen();
        var itemHeight = this.GetItemHeightAlignedToPixel(screen);
        var itemWidth = finalSize.Width;
        var containerY = this.firstRealizedIndex * itemHeight;
        for (int i = 0, realizedContainerCount = this.realizedContainers.Count; i < realizedContainerCount; ++i)
        {
            var container = this.realizedContainers[i].AsNonNull();
            container.Arrange(new(0, AlignToPixels(containerY, screen), itemWidth, itemHeight));
            containerY += itemHeight;
        }
        return finalSize;
    }


    /// <inheritdoc/>
    protected override Control? ContainerFromIndex(int index)
    {
        if (index < 0)
            return null;
        if (this.realizingContainerIndex >= 0 && index == this.realizingContainerIndex)
            return this.realizingContainer;
        if (index < this.firstRealizedIndex || index > this.lastRealizedIndex)
            return null;
        var container = this.realizedContainers[index - this.firstRealizedIndex];
        if (container is null)
        {
            container = this.GetOrCreateContainer(this.Items, index);
            this.realizedContainers[index - this.firstRealizedIndex] = container;
        }
        return container;
    }
    

    /// <inheritdoc/>
    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        return null;
    }
    
    
    // Get item height which is aligned to physical pixel.
    double GetItemHeightAlignedToPixel(Screen? screen) =>
        AlignToPixels(this.ItemHeight, screen);


    // Get or create container for item.
    Control GetOrCreateContainer(IReadOnlyList<object?> items, int index)
    {
        // use realized container
        Control? container;
        if (index >= this.firstRealizedIndex && index <= this.lastRealizedIndex)
        {
            container = this.realizedContainers[index - this.firstRealizedIndex];
            if (container is not null)
                return container;
        }

        // create container
        var item = items[index];
        var generator = this.ItemContainerGenerator.AsNonNull();
        if (generator.NeedsContainer(item, index, out var recycleKey))
        {
            if (recycleKey is null || !this.TryGetRecycledContainer(recycleKey, out container))
            {
                container = generator.CreateContainer(item, index, recycleKey);
                container.SetValue(RecycleKeyProperty, recycleKey);
                this.AddInternalChild(container);
            }
        }
        else
        {
            container = (Control)item!;
            container.SetValue(RecycleKeyProperty, recycleKey);
            this.AddInternalChild(container);
        }

        // prepare container
        this.realizingContainer = container;
        this.realizingContainerIndex = index;
        generator.PrepareItemContainer(container, item, index);
        generator.ItemContainerPrepared(container, item, index);
        this.realizingContainer = null;
        this.realizingContainerIndex = -1;
        
        // complete
        return container;
    }


    /// <inheritdoc/>
    protected override IEnumerable<Control> GetRealizedContainers() =>
        (IEnumerable<Control>)this.realizedContainers.Where(it => it is not null);


    // Get screen which contains the window.
    Screen? GetScreen() =>
        this.window?.Screens.Let(screens => screens.ScreenFromWindow(this.window) ?? screens.Primary);
    
    
    /// <inheritdoc/>
    protected override int IndexFromContainer(Control container)
    {
        if (container == this.realizingContainer)
            return this.realizingContainerIndex;
        var containers = this.realizedContainers;
        for (var i = containers.Count - 1; i >= 0; --i)
        {
            if (containers[i] == container)
                return this.firstRealizedIndex + i;
        }
        return -1;
    }


    /// <summary>
    /// Get or set height of each item.
    /// </summary>
    public double ItemHeight
    {
        get => this.GetValue(ItemHeightProperty);
        set => this.SetValue(ItemHeightProperty, value);
    }


    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        // get state
        var items = this.Items;
        var itemCount = items.Count;
        var itemHeight = this.GetItemHeightAlignedToPixel(this.GetScreen());
        var viewport = this.scrollViewer?.Viewport ?? default;
        var offset = this.scrollViewer?.Offset ?? default;
        
        // ignore measurement
        if (itemCount <= 0 || itemHeight <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
            return default;
        
        // calculate visible range
        var firstVisibleIndex = Math.Max(0, (int)(offset.Y / itemHeight));
        var lastVisibleIndex = Math.Min(itemCount - 1, (int)(Math.Ceiling((offset.Y + viewport.Height) / itemHeight) + 0.1));
        
        // recycle containers
        if (this.firstRealizedIndex >= 0)
        {
            if (this.firstRealizedIndex > lastVisibleIndex || this.lastRealizedIndex < firstVisibleIndex)
            {
                for (var i = this.realizedContainers.Count - 1; i >= 0; --i)
                {
                    var container = this.realizedContainers[i];
                    if (container is not null)
                    {
                        var recycleKey = container.GetValue(RecycleKeyProperty);
                        this.RecycleContainer(recycleKey, container);
                    }
                }
                this.realizedContainers.Clear();
                this.firstRealizedIndex = -1;
                this.lastRealizedIndex = -1;
            }
            else
            {
                if (this.firstRealizedIndex < firstVisibleIndex)
                {
                    var recycleCount = (firstVisibleIndex - this.firstRealizedIndex);
                    for (var i = recycleCount - 1; i >= 0; --i)
                    {
                        var container = this.realizedContainers[i];
                        if (container is not null)
                        {
                            var recycleKey = container.GetValue(RecycleKeyProperty);
                            this.RecycleContainer(recycleKey, container);
                        }
                    }
                    this.realizedContainers.RemoveRange(0, recycleCount);
                    this.firstRealizedIndex = firstVisibleIndex;
                }
                if (this.lastRealizedIndex > lastVisibleIndex)
                {
                    var recycleCount = (this.lastRealizedIndex - lastVisibleIndex);
                    for (var i = recycleCount; i > 0; --i)
                    {
                        var container = this.realizedContainers[^i];
                        if (container is not null)
                        {
                            var recycleKey = container.GetValue(RecycleKeyProperty);
                            this.RecycleContainer(recycleKey, container);
                        }
                    }
                    this.realizedContainers.RemoveRange(this.realizedContainers.Count - recycleCount, recycleCount);
                    this.lastRealizedIndex = lastVisibleIndex;
                }
            }
        }
        
        // create containers
        if (!this.invalidateMeasureAction.IsScheduled)
        {
            var startTime = Stopwatch.ElapsedMilliseconds;
            var maxDuration = this.app?.Configuration.GetValueOrDefault(MaxRealizingContainersDuration) ?? 66;
            if (this.firstRealizedIndex < 0)
            {
                for (var index = firstVisibleIndex; index <= lastVisibleIndex; ++index)
                {
                    // realize container
                    var container = this.GetOrCreateContainer(items, index);
                    this.realizedContainers.Add(container);
                    
                    // abort if the duration exceeds the limit
                    if ((Stopwatch.ElapsedMilliseconds - startTime) >= maxDuration && index < lastVisibleIndex)
                    {
                        lastVisibleIndex = index;
                        this.invalidateMeasureAction.Schedule(this.app?.Configuration.GetValueOrDefault(MinRealizingContainersInterval) ?? 66);
                        break;
                    }
                }
            }
            else
            {
                if (this.firstRealizedIndex > firstVisibleIndex)
                {
                    for (int index = this.firstRealizedIndex - 1; index >= firstVisibleIndex; --index)
                    {
                        // realize container
                        var container = this.GetOrCreateContainer(items, index);
                        this.realizedContainers.Insert(0, container);
                        
                        // abort if the duration exceeds the limit
                        if ((Stopwatch.ElapsedMilliseconds - startTime) >= maxDuration && index > firstVisibleIndex)
                        {
                            firstVisibleIndex = index;
                            this.invalidateMeasureAction.Schedule(this.app?.Configuration.GetValueOrDefault(MinRealizingContainersInterval) ?? 66);
                            break;
                        }
                    }
                }
                if (this.lastRealizedIndex < lastVisibleIndex)
                {
                    if ((Stopwatch.ElapsedMilliseconds - startTime) < maxDuration)
                    {
                        for (int index = this.lastRealizedIndex + 1; index <= lastVisibleIndex; ++index)
                        {
                            // realize container
                            var container = this.GetOrCreateContainer(items, index);
                            this.realizedContainers.Add(container);
                            
                            // abort if the duration exceeds the limit
                            if ((Stopwatch.ElapsedMilliseconds - startTime) >= maxDuration && index < lastVisibleIndex)
                            {
                                lastVisibleIndex = index;
                                this.invalidateMeasureAction.Schedule(this.app?.Configuration.GetValueOrDefault(MinRealizingContainersInterval) ?? 66);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // abort because the duration exceeds the limit
                        lastVisibleIndex = this.lastRealizedIndex;
                        this.invalidateMeasureAction.Schedule(this.app?.Configuration.GetValueOrDefault(MinRealizingContainersInterval) ?? 66);
                    }
                }
            }
            this.firstRealizedIndex = firstVisibleIndex;
            this.lastRealizedIndex = lastVisibleIndex;
        }

        // measure containers
        var maxContainerWidth = 0.0;
        var isScrollBarEnabled = this.scrollViewer?.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        for (var i = this.realizedContainers.Count - 1; i >= 0; --i)
        {
            var container = this.realizedContainers[i];
            if (container is null)
            {
                container = this.GetOrCreateContainer(items, this.firstRealizedIndex + i);
                this.realizedContainers[i] = container;
            }
            if (isScrollBarEnabled)
                container.Measure(new(double.PositiveInfinity, itemHeight));
            else
                container.Measure(new(availableSize.Width, itemHeight));
            maxContainerWidth = Math.Max(maxContainerWidth, container.DesiredSize.Width);
        }
        
        // complete
        if (!isScrollBarEnabled && double.IsFinite(availableSize.Width))
            return new(availableSize.Width, itemHeight * itemCount);
        return new(maxContainerWidth, itemHeight * itemCount);
    }


    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        this.app = IAppSuiteApplication.CurrentOrNull;
        this.scrollViewer = this.FindAncestorOfType<ScrollViewer>()?.Also(it =>
        {
            this.scrollViewerOffsetObserverToken = it.GetObservable(ScrollViewer.OffsetProperty).Subscribe(this.InvalidateMeasure);
            this.scrollViewerViewportObserverToken = it.GetObservable(ScrollViewer.ViewportProperty).Subscribe(this.InvalidateMeasure);
        });
        this.window = TopLevel.GetTopLevel(this) as Window;
    }


    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        this.app = null;
        this.scrollViewerOffsetObserverToken = this.scrollViewerOffsetObserverToken.DisposeAndReturnNull();
        this.scrollViewerViewportObserverToken = this.scrollViewerViewportObserverToken.DisposeAndReturnNull();
        this.scrollViewer = null;
        this.window = null;
        this.invalidateMeasureAction.Cancel();
        base.OnDetachedFromVisualTree(e);
    }
    
    
    // Called when items added.
    void OnItemsAdded(NotifyCollectionChangedEventArgs e)
    {
        // check state
        var itemCount = e.NewItems?.Count ?? 0;
        var firstAddedIndex = e.NewStartingIndex;
        var lastAddedIndex = firstAddedIndex + itemCount - 1;
        if (this.firstRealizedIndex < 0)
            return;
        
        // items added below viewport
        if (firstAddedIndex > this.lastRealizedIndex)
            return;
        
        // items added above viewport
        if (firstAddedIndex <= this.firstRealizedIndex)
        {
            this.firstRealizedIndex += itemCount;
            this.lastRealizedIndex += itemCount;
            if (this.scrollViewer is not null)
            {
                var screen = this.GetScreen();
                var itemHeight = this.GetItemHeightAlignedToPixel(screen);
                if (itemHeight > 0)
                {
                    var addedHeight = itemCount * itemHeight;
                    var offset = this.scrollViewer.Offset;
                    this.scrollViewer.Offset = new(offset.X, AlignToPixels(offset.Y + addedHeight, screen));
                }
            }
            return;
        }
        
        // all items in viewport were moved below viewport
        if (firstAddedIndex <= this.firstRealizedIndex && lastAddedIndex >= this.lastRealizedIndex)
        {
            this.RecycleAllContainers();
            return;
        }
        
        // part of items in viewport were moved down
        firstAddedIndex = Math.Max(firstAddedIndex, this.firstRealizedIndex);
        lastAddedIndex = Math.Min(lastAddedIndex, this.lastRealizedIndex);
        itemCount = lastAddedIndex - firstAddedIndex + 1;
        if (itemCount == 1)
            this.realizedContainers.Insert(firstAddedIndex - this.firstRealizedIndex, null);
        else
            this.realizedContainers.InsertRange(firstAddedIndex - this.firstRealizedIndex, new Control?[itemCount]);
        this.lastRealizedIndex += itemCount;
    }


    /// <inheritdoc/>
    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.OnItemsAdded(e);
                break;
            case NotifyCollectionChangedAction.Move:
                this.OnItemsMoved(e);
                break;
            case NotifyCollectionChangedAction.Remove:
                this.OnItemsRemoved(e);
                break;
            case NotifyCollectionChangedAction.Replace:
                this.OnItemsReplaced(e);
                break;
            case NotifyCollectionChangedAction.Reset:
                this.RecycleAllContainers();
                break;
        }
        this.InvalidateMeasure();
    }
    
    
    // Called when items moved.
    void OnItemsMoved(NotifyCollectionChangedEventArgs e)
    {
        throw new NotSupportedException();
    }
    
    
    // Called when items removed.
    void OnItemsRemoved(NotifyCollectionChangedEventArgs e)
    {
        // check state
        var itemCount = e.OldItems?.Count ?? 0;
        var firstRemovedIndex = e.OldStartingIndex;
        var lastRemovedIndex = firstRemovedIndex + itemCount - 1;
        if (this.firstRealizedIndex < 0)
            return;
        
        // items removed below viewport
        if (firstRemovedIndex > this.lastRealizedIndex)
            return;
        
        // items removed above viewport
        if (lastRemovedIndex < this.firstRealizedIndex)
        {
            this.firstRealizedIndex -= itemCount;
            this.lastRealizedIndex -= itemCount;
            if (this.scrollViewer is not null)
            {
                var screen = this.GetScreen();
                var itemHeight = this.GetItemHeightAlignedToPixel(screen);
                if (itemHeight > 0)
                {
                    var removedHeight = itemCount * itemHeight;
                    var offset = this.scrollViewer.Offset;
                    this.scrollViewer.Offset = new(offset.X, Math.Max(0, AlignToPixels(offset.Y - removedHeight, screen)));
                }
            }
            return;
        }
        
        // all items in viewport were removed
        if (firstRemovedIndex <= this.firstRealizedIndex && lastRemovedIndex >= this.lastRealizedIndex)
        {
            this.RecycleAllContainers();
            return;
        }
        
        // part of items in viewport were removed
        firstRemovedIndex = Math.Max(firstRemovedIndex, this.firstRealizedIndex);
        lastRemovedIndex = Math.Min(lastRemovedIndex, this.lastRealizedIndex);
        itemCount = lastRemovedIndex - firstRemovedIndex + 1;
        for (var index = firstRemovedIndex; index <= lastRemovedIndex; ++index)
            this.RecycleContainer(this.realizedContainers[index - this.firstRealizedIndex]);
        this.realizedContainers.RemoveRange(firstRemovedIndex - this.firstRealizedIndex, itemCount);
        this.lastRealizedIndex -= itemCount;
    }
    
    
    // Called when items replaced.
    void OnItemsReplaced(NotifyCollectionChangedEventArgs e)
    {
        // check state
        var itemCount = e.OldItems?.Count ?? 0;
        var firstReplacedIndex = e.OldStartingIndex;
        var lastReplacedIndex = firstReplacedIndex + itemCount - 1;
        if (this.firstRealizedIndex < 0)
            return;

        // items replaced out of viewport
        if (firstReplacedIndex > this.lastRealizedIndex || lastReplacedIndex < this.firstRealizedIndex)
            return;
        
        // all items in viewport were replaced
        if (firstReplacedIndex <= this.firstRealizedIndex && lastReplacedIndex >= this.lastRealizedIndex)
        {
            this.RecycleAllContainers();
            return;
        }
        
        // part of items in viewport were replaced
        firstReplacedIndex = Math.Max(firstReplacedIndex, this.firstRealizedIndex);
        lastReplacedIndex = Math.Min(lastReplacedIndex, this.lastRealizedIndex);
        for (var index = firstReplacedIndex; index <= lastReplacedIndex; ++index)
        {
            var containerIndex = index - this.firstRealizedIndex;
            this.RecycleContainer(this.realizedContainers[containerIndex]);
            this.realizedContainers[containerIndex] = null;
        }
    }


    // Recycle all containers.
    void RecycleAllContainers()
    {
        for (var i = this.realizedContainers.Count - 1; i >= 0; --i)
        {
            var container = this.realizedContainers[i];
            if (container is not null)
            {
                var recycleKey = container.GetValue(RecycleKeyProperty);
                this.RecycleContainer(recycleKey, container);
            }
        }
        this.realizedContainers.Clear();
        this.firstRealizedIndex = -1;
        this.lastRealizedIndex = -1;
    }


    // Recycle container.
    void RecycleContainer(Control? container)
    {
        if (container is not null)
            this.RecycleContainer(container.GetValue(RecycleKeyProperty), container);
    }
    void RecycleContainer(object? recycleKey, Control container)
    {
        if (recycleKey is null)
        {
            this.RemoveInternalChild(container);
            this.ItemContainerGenerator?.ClearItemContainer(container);
            return;
        }
        if (this.recycledContainers.TryGetValue(recycleKey, out var containers))
            containers.Push(container);
        else
        {
            containers = new();
            containers.Push(container);
            this.recycledContainers[recycleKey] = containers;
        }
        container.IsVisible = false;
        this.ItemContainerGenerator?.ClearItemContainer(container);
    }


    /// <inheritdoc/>
    protected override Control? ScrollIntoView(int index)
    {
        // check state and parameter
        if (this.firstRealizedIndex < 0)
            return null;
        var items = this.Items;
        if (index < 0 || index >= items.Count)
            return null;
        
        // scroll to realizing/realized container
        if (index >= this.firstRealizedIndex && index <= this.lastRealizedIndex)
        {
            var container = this.realizedContainers[index - this.firstRealizedIndex];
            if (container is null)
            {
                container = this.GetOrCreateContainer(this.Items, index);
                this.realizedContainers[index - this.firstRealizedIndex] = container;
            }
            container.BringIntoView();
            return container;
        }
        if (index == this.realizingContainerIndex)
        {
            this.realizingContainer?.BringIntoView();
            return this.realizingContainer;
        }
        
        // scroll to item
        if (this.scrollViewer is null)
            return null;
        var screen = this.GetScreen();
        var itemHeight = this.GetItemHeightAlignedToPixel(screen);
        if (itemHeight <= 0)
            return null;
        var containerY = index * itemHeight;
        var offset = this.scrollViewer.Offset;
        if (index < this.firstRealizedIndex) // scroll up
            offset = new(offset.X, containerY);
        else // scroll down
        {
            var viewport = this.scrollViewer.Viewport;
            var isScrollBarVisible = this.scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;
            offset = new(offset.X, Math.Max(0, AlignToPixels(containerY + itemHeight + (isScrollBarVisible ? 20 : 0) - viewport.Height, screen)));
        }
        this.scrollViewer.Offset = offset;
        return null;
    }


    // Try getting recycled containers.
    bool TryGetRecycledContainer(object recycleKey, [NotNullWhen(true)] out Control? container)
    {
        if (this.recycledContainers.TryGetValue(recycleKey, out var containers) && containers.TryPop(out container))
        {
            container.IsVisible = true;
            return true;
        }
        container = null;
        return false;
    }
}