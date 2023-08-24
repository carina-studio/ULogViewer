using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CarinaStudio.ULogViewer.Logs.DataSources;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ContextMenu"/> to select <see cref="ScriptLogDataSourceProvider"/>.
/// </summary>
class ScriptLogDataSourceProviderSelectionContextMenu : ContextMenu
{
    // Fields.
    MenuItem? emptyMenuItem;
    readonly SortedObservableList<MenuItem> menuItems;
    readonly PropertyChangedEventHandler providerPropertyChangedHandler;
    readonly Dictionary<ScriptLogDataSourceProvider, IDisposable> providerPropertyChangedHandlerTokens = new();
    readonly NotifyCollectionChangedEventHandler providersChangedHandler;
    IDisposable? providersChangedHandlerToken;
    

    /// <summary>
    /// Initialize new <see cref="ScriptLogDataSourceProviderSelectionContextMenu"/> instance.
    /// </summary>
    public ScriptLogDataSourceProviderSelectionContextMenu()
    {
        this.menuItems = new(this.CompareMenuItems);
        this.providerPropertyChangedHandler = this.OnProviderPropertyChanged;
        this.providersChangedHandler = this.OnProvidersChanged;
        base.ItemsSource = this.menuItems;
    }
    
    
    // Finalizer.
    ~ScriptLogDataSourceProviderSelectionContextMenu()
    {
        foreach (var token in this.providerPropertyChangedHandlerTokens.Values)
            token.Dispose();
        this.providersChangedHandlerToken?.Dispose();
    }
    
    
    // Compare menu items.
    int CompareMenuItems(MenuItem lhs, MenuItem rhs)
    {
        var lhsProvider = lhs.DataContext as ScriptLogDataSourceProvider;
        var rhsProvider = rhs.DataContext as ScriptLogDataSourceProvider;
        if (lhsProvider is null)
            return -1;
        if (rhsProvider is null)
            return 1;
        var result = string.Compare(lhsProvider.DisplayName, rhsProvider.DisplayName, StringComparison.InvariantCulture);
        return result != 0 ? result : string.Compare(lhsProvider.Name, rhsProvider.Name, StringComparison.InvariantCulture);
    }
    
    
    // Create menu item for empty provider.
    MenuItem CreateEmptyMenuItem() => new MenuItem().Also(it =>
    {
        it.BindToResource(MenuItem.HeaderProperty, this, "String/Common.None");
        it.IsEnabled = false;
    });
    
    
    // Create menu item for provider.
    MenuItem CreateMenuItem(ScriptLogDataSourceProvider provider) => new MenuItem().Also(it =>
    {
        it.Click += (_, _) => this.SelectProvider(provider);
        it.DataContext = provider;
        it.Bind(MenuItem.HeaderProperty, new Binding { Path = nameof(ScriptLogDataSourceProvider.DisplayName) });
        it.Icon = new Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.BindToResource(Image.SourceProperty, this, "Image/Code");
        });
    });
    
    
    /// <inheritdoc cref="ItemsControl.ItemsSource"/>.
    public new IEnumerable? ItemsSource => base.ItemsSource;


    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (this.providersChangedHandlerToken is null)
        {
            LogDataSourceProviders.ScriptProviders.Let(providers =>
            {
                if (providers is INotifyCollectionChanged notifyCollectionChanged)
                    this.providersChangedHandlerToken = notifyCollectionChanged.AddWeakCollectionChangedEventHandler(this.providersChangedHandler);
                if (providers.Count == 0)
                {
                    this.emptyMenuItem = this.CreateEmptyMenuItem();
                    this.menuItems.Add(this.emptyMenuItem);
                }
                else
                {
                    foreach (var provider in providers)
                    {
                        this.providerPropertyChangedHandlerTokens[provider] = provider.AddWeakPropertyChangedEventHandler(this.providerPropertyChangedHandler);
                        this.menuItems.Add(this.CreateMenuItem(provider));
                    }
                }
            });
        }
    }


    // Called when property of provider changed.
    void OnProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ScriptLogDataSourceProvider provider)
            return;
        if (e.PropertyName != nameof(ScriptLogDataSourceProvider.DisplayName))
            return;
        for (var i = this.menuItems.Count - 1; i >= 0; --i)
        {
            if (this.menuItems[i].DataContext == provider)
            {
                this.menuItems.SortAt(i);
                break;
            }
        }
    }
    
    
    // Called when collection of providers changed.
    void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (this.emptyMenuItem is not null && this.menuItems.IsNotEmpty() && this.menuItems[0] == this.emptyMenuItem)
                    this.menuItems.RemoveAt(0);
                foreach (var provider in e.NewItems!.Cast<ScriptLogDataSourceProvider>())
                {
                    this.providerPropertyChangedHandlerTokens[provider] = provider.AddWeakPropertyChangedEventHandler(this.providerPropertyChangedHandler);
                    this.menuItems.Add(this.CreateMenuItem(provider));
                }
                break;
            case NotifyCollectionChangedAction.Move:
                e.OldItems!.Cast<ScriptLogDataSourceProvider>().Let(movedProviders =>
                {
                    if (movedProviders.Count != 1)
                        throw new NotSupportedException();
                    var provider = movedProviders[0];
                    for (var i = this.menuItems.Count - 1; i >= 0; --i)
                    {
                        if (this.menuItems[i].DataContext == provider)
                        {
                            this.menuItems.SortAt(i);
                            break;
                        }
                    }
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<ScriptLogDataSourceProvider>().Let(removedProviders =>
                {
                    foreach (var provider in removedProviders)
                    {
                        if (this.providerPropertyChangedHandlerTokens.TryGetValue(provider, out var token))
                        {
                            token.Dispose();
                            this.providerPropertyChangedHandlerTokens.Remove(provider);
                        }
                    }
                    this.menuItems.RemoveAll(it =>
                    {
                        if (it.DataContext is not ScriptLogDataSourceProvider provider)
                            return false;
                        return removedProviders.Contains(provider);
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
                foreach (var token in this.providerPropertyChangedHandlerTokens.Values)
                    token.Dispose();
                this.providerPropertyChangedHandlerTokens.Clear();
                LogDataSourceProviders.ScriptProviders.Let(providers =>
                {
                    if (providers.Count == 0)
                    {
                        this.emptyMenuItem = this.CreateEmptyMenuItem();
                        this.menuItems.Add(this.emptyMenuItem);
                    }
                    else
                    {
                        foreach (var provider in providers)
                        {
                            this.providerPropertyChangedHandlerTokens[provider] = provider.AddWeakPropertyChangedEventHandler(this.providerPropertyChangedHandler);
                            this.menuItems.Add(this.CreateMenuItem(provider));
                        }
                    }
                });
                break;
            default:
                throw new NotSupportedException($"Unsupported action of change of collection: {e.Action}.");
        }
    }


    /// <summary>
    /// Open the menu to let user select a <see cref="ScriptLogDataSourceProvider"/>.
    /// </summary>
    /// <param name="control">Control.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of opening and waiting for user selection.</returns>
    public async Task<ScriptLogDataSourceProvider?> OpenAsync(Control control, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;
        if (this.IsOpen)
            this.Close();
        var taskCompletionSource = new TaskCompletionSource<ScriptLogDataSourceProvider?>();
        void OnClosed(object? sender, EventArgs e) => 
            Dispatcher.UIThread.Post(() => taskCompletionSource.TrySetResult(null));
        void OnProviderSelected(ScriptLogDataSourceProviderSelectionContextMenu menu, ScriptLogDataSourceProvider provider) =>
            taskCompletionSource.TrySetResult(provider);
        var registration = cancellationToken.Register(this.Close);
        this.Closed += OnClosed;
        this.ProviderSelected += OnProviderSelected;
        this.Open(control);
        try
        {
            return await taskCompletionSource.Task;
        }
        finally
        {
            registration.Dispose();
            this.Closed -= OnClosed;
            this.ProviderSelected -= OnProviderSelected;
        }
    }
    
    
    /// <summary>
    /// Raised when a <see cref="ScriptLogDataSourceProvider"/> has been selected.
    /// </summary>
    public event Action<ScriptLogDataSourceProviderSelectionContextMenu, ScriptLogDataSourceProvider>? ProviderSelected;
    
    
    // Select given provider.
    void SelectProvider(ScriptLogDataSourceProvider provider)
    {
        this.Close();
        this.ProviderSelected?.Invoke(this, provider);
    }
    
    
    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ContextMenu);
}