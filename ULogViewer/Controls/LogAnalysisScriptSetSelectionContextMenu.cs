using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ContextMenu"/> to select <see cref="LogAnalysisScriptSet"/>.
/// </summary>
class LogAnalysisScriptSetSelectionContextMenu : ContextMenu
{
    // Fields.
    MenuItem? emptyMenuItem;
    LogAnalysisScriptSetManager? logAnalysisScriptSetManager;
    readonly PropertyChangedEventHandler logAnalysisScriptSetPropertyChangedHandler;
    readonly Dictionary<LogAnalysisScriptSet, IDisposable> logAnalysisScriptSetPropertyChangedHandlerTokens = new();
    readonly NotifyCollectionChangedEventHandler logAnalysisScriptSetsChangedHandler;
    IDisposable? logAnalysisScriptSetsChangedHandlerToken;
    readonly SortedObservableList<MenuItem> menuItems;


    /// <summary>
    /// Initialize new <see cref="LogAnalysisScriptSetSelectionContextMenu"/> instance.
    /// </summary>
    public LogAnalysisScriptSetSelectionContextMenu()
    {
        this.logAnalysisScriptSetPropertyChangedHandler = this.OnLogAnalysisScriptSetPropertyChanged;
        this.logAnalysisScriptSetsChangedHandler = this.OnLogAnalysisScriptSetsChanged;
        this.menuItems = new(this.CompareMenuItems);
        base.ItemsSource = this.menuItems;
    }
    
    
    // Finalizer.
    ~LogAnalysisScriptSetSelectionContextMenu()
    {
        foreach (var token in this.logAnalysisScriptSetPropertyChangedHandlerTokens.Values)
            token.Dispose();
        this.logAnalysisScriptSetsChangedHandlerToken?.Dispose();
        this.logAnalysisScriptSetManager = null;
    }
    
    
    // Compare menu items.
    int CompareMenuItems(MenuItem lhs, MenuItem rhs)
    {
        var lhsScriptSet = lhs.DataContext as LogAnalysisScriptSet;
        var rhsScriptSet = rhs.DataContext as LogAnalysisScriptSet;
        if (lhsScriptSet is null)
            return -1;
        if (rhsScriptSet is null)
            return 1;
        var result = string.Compare(lhsScriptSet.Name, rhs.Name, StringComparison.InvariantCulture);
        return result != 0 ? result : string.Compare(lhsScriptSet.Id, rhsScriptSet.Id, StringComparison.InvariantCulture);
    }
    
    
    // Create menu item for empty log analysis script set.
    MenuItem CreateEmptyMenuItem() => new MenuItem().Also(it =>
    {
        it.BindToResource(MenuItem.HeaderProperty, this, "String/Common.None");
        it.IsEnabled = false;
    });
    
    
    // Create menu item for log analysis script set.
    MenuItem CreateMenuItem(LogAnalysisScriptSet scriptSet) => new MenuItem().Also(it =>
    {
        it.Click += (_, _) => this.SelectLogAnalysisScriptSet(scriptSet);
        it.DataContext = scriptSet;
        it.Bind(MenuItem.HeaderProperty, new Binding { Path = nameof(LogAnalysisScriptSet.Name) });
        it.Icon = new Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Bind(Image.SourceProperty, new MultiBinding
            {
                Bindings =
                {
                    new Binding { Path = nameof(LogAnalysisScriptSet.Icon) },
                    new Binding { Path = nameof(LogAnalysisScriptSet.IconColor) }
                }, 
                Converter = LogProfileIconConverter.Default
            });
        });
    });
    
    
    /// <inheritdoc cref="ItemsControl.ItemsSource"/>.
    public new IEnumerable? ItemsSource => base.ItemsSource;


    /// <summary>
    /// Raised when a <see cref="LogAnalysisScriptSet"/> has been selected.
    /// </summary>
    public event Action<LogAnalysisScriptSetSelectionContextMenu, LogAnalysisScriptSet>? LogAnalysisScriptSetSelected;


    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (this.logAnalysisScriptSetManager is null)
        {
            this.logAnalysisScriptSetManager = LogAnalysisScriptSetManager.Default;
            this.logAnalysisScriptSetManager.ScriptSets.Let(scriptSets =>
            {
                if (scriptSets is INotifyCollectionChanged notifyCollectionChanged)
                    this.logAnalysisScriptSetsChangedHandlerToken = notifyCollectionChanged.AddWeakCollectionChangedEventHandler(this.logAnalysisScriptSetsChangedHandler);
                if (scriptSets.Count == 0)
                {
                    this.emptyMenuItem = this.CreateEmptyMenuItem();
                    this.menuItems.Add(this.emptyMenuItem);
                }
                else
                {
                    foreach (var scriptSet in scriptSets)
                    {
                        this.logAnalysisScriptSetPropertyChangedHandlerTokens[scriptSet] = scriptSet.AddWeakPropertyChangedEventHandler(this.logAnalysisScriptSetPropertyChangedHandler);
                        this.menuItems.Add(this.CreateMenuItem(scriptSet));
                    }
                }
            });
        }
    }


    // Called when property of log analysis script changed.
    void OnLogAnalysisScriptSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LogAnalysisScriptSet scriptSet)
            return;
        if (e.PropertyName != nameof(LogAnalysisScriptSet.Name))
            return;
        for (var i = this.menuItems.Count - 1; i >= 0; --i)
        {
            if (this.menuItems[i].DataContext == scriptSet)
            {
                this.menuItems.SortAt(i);
                break;
            }
        }
    }
    
    
    // Called when collection of log analysis script sets changed.
    void OnLogAnalysisScriptSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.logAnalysisScriptSetManager is null)
            return;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (this.emptyMenuItem is not null && this.menuItems.IsNotEmpty() && this.menuItems[0] == this.emptyMenuItem)
                    this.menuItems.RemoveAt(0);
                foreach (var scriptSet in e.NewItems!.Cast<LogAnalysisScriptSet>())
                {
                    this.logAnalysisScriptSetPropertyChangedHandlerTokens[scriptSet] = scriptSet.AddWeakPropertyChangedEventHandler(this.logAnalysisScriptSetPropertyChangedHandler);
                    this.menuItems.Add(this.CreateMenuItem(scriptSet));
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<LogAnalysisScriptSet>().Let(removedScriptSets =>
                {
                    foreach (var scriptSet in removedScriptSets)
                    {
                        if (this.logAnalysisScriptSetPropertyChangedHandlerTokens.TryGetValue(scriptSet, out var token))
                        {
                            token.Dispose();
                            this.logAnalysisScriptSetPropertyChangedHandlerTokens.Remove(scriptSet);
                        }
                    }
                    this.menuItems.RemoveAll(it =>
                    {
                        if (it.DataContext is not LogAnalysisScriptSet scriptSet)
                            return false;
                        return removedScriptSets.Contains(scriptSet);
                    });
                    if (this.menuItems.IsEmpty())
                    {
                        this.emptyMenuItem ??= this.CreateEmptyMenuItem();
                        this.menuItems.Add(this.emptyMenuItem);
                    }
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                this.menuItems.Clear();
                foreach (var token in this.logAnalysisScriptSetPropertyChangedHandlerTokens.Values)
                    token.Dispose();
                this.logAnalysisScriptSetPropertyChangedHandlerTokens.Clear();
                this.logAnalysisScriptSetManager.ScriptSets.Let(scriptSets =>
                {
                    if (scriptSets.Count == 0)
                    {
                        this.emptyMenuItem = this.CreateEmptyMenuItem();
                        this.menuItems.Add(this.emptyMenuItem);
                    }
                    else
                    {
                        foreach (var scriptSet in scriptSets)
                        {
                            this.logAnalysisScriptSetPropertyChangedHandlerTokens[scriptSet] = scriptSet.AddWeakPropertyChangedEventHandler(this.logAnalysisScriptSetPropertyChangedHandler);
                            this.menuItems.Add(this.CreateMenuItem(scriptSet));
                        }
                    }
                });
                break;
            default:
                throw new NotSupportedException($"Unsupported action of change of collection: {e.Action}.");
        }
    }
    
    
    // Select given script set.
    void SelectLogAnalysisScriptSet(LogAnalysisScriptSet scriptSet)
    {
        this.Close();
        this.LogAnalysisScriptSetSelected?.Invoke(this, scriptSet);
    }
    
    
    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ContextMenu);
}