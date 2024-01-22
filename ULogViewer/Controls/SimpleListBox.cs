using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="Avalonia.Controls.ListBox"/> which is optimized for performance.
/// </summary>
public class SimpleListBox : CarinaStudio.AppSuite.Controls.ListBox
{
    // Constants.
    const int ClearContainerContentDelay = 100;
    const int ViewportChangeTimeout = 500;
    
    
    // Static fields.
    static DispatcherSynchronizationContext? UISyncContext;
    
    
    // Fields.
    readonly ScheduledAction clearContainerContentAction;
    readonly ScheduledAction clearIsChangingViewportAction;
    readonly HashSet<Control> containersToClearContent = new();
    IDisposable? firstVisibleIndexObserverToken;
    readonly FieldInfo ignoreContainerSelectionChangedField;
    bool isChangingViewport;
    IDataTemplate? itemTemplate;


    /// <summary>
    /// Initialize new <see cref="SimpleListBox"/> instance.
    /// </summary>
    public SimpleListBox()
    {
        UISyncContext ??= new(Dispatcher.UIThread);
        this.clearContainerContentAction = new DispatcherScheduledAction(UISyncContext, this.ClearContainerContent, DispatcherPriority.Background);
        this.clearIsChangingViewportAction = new DispatcherScheduledAction(UISyncContext, () =>
        {
            if (this.isChangingViewport)
            {
                this.isChangingViewport = false;
                if (this.containersToClearContent.IsNotEmpty())
                    this.clearContainerContentAction.Schedule();
            }
        });
        this.ignoreContainerSelectionChangedField = typeof(SelectingItemsControl).GetField("_ignoreContainerSelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic).AsNonNull();
        this.GetObservable(ItemTemplateProperty).Subscribe(itemTemplate => this.itemTemplate = itemTemplate);
    }
    
    
    // Clear content of containers.
    void ClearContainerContent()
    {
        if (this.containersToClearContent.IsEmpty() || this.isChangingViewport)
            return;
        foreach (var container in this.containersToClearContent)
        {
            base.ClearContainerForItemOverride(container);
            this.containersToClearContent.Remove(container);
            break;
        }
        if (this.containersToClearContent.IsNotEmpty())
            this.clearContainerContentAction.Schedule();
    }
    
    
    /// <inheritdoc/>
    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is ListBoxItem listBoxItem && this.ItemCount > 0)
        {
            this.ignoreContainerSelectionChangedField.SetValue(this, true);
            try
            {
                listBoxItem.ClearValue(ListBoxItem.IsSelectedProperty);
            }
            finally
            {
                this.ignoreContainerSelectionChangedField.SetValue(this, false);
            }
            if (this.containersToClearContent.Add(container))
                this.clearContainerContentAction.Schedule(ClearContainerContentDelay);
        }
        else
            base.ClearContainerForItemOverride(container);
    }


    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        this.firstVisibleIndexObserverToken = this.firstVisibleIndexObserverToken.DisposeAndReturnNull();
        base.OnApplyTemplate(e);
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindDescendantOfType<SimpleVirtualizingStackPanel>() is { } panel)
            {
                var observer = new Action<int>(_ =>
                {
                    this.isChangingViewport = true;
                    this.clearIsChangingViewportAction.Reschedule(ViewportChangeTimeout);
                });
                this.firstVisibleIndexObserverToken = panel.GetObservable(SimpleVirtualizingStackPanel.FirstVisibleIndexProperty).Subscribe(observer);
            }
            else
                this.clearIsChangingViewportAction.ExecuteIfScheduled();
        }, DispatcherPriority.Default);
    }


    /// <inheritdoc/>
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        this.containersToClearContent.Remove(container);
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is ListBoxItem listBoxItem)
        {
            listBoxItem.SetCurrentValue(ListBoxItem.ContentProperty, item);
            listBoxItem.SetCurrentValue(ListBoxItem.ContentTemplateProperty, this.itemTemplate);
        }
    }
}