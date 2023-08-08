using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Globalization;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ContextMenu"/> to select log profile.
/// </summary>
class LogProfileSelectionContextMenu : ContextMenu
{
    /// <summary>
    /// Property of <see cref="CurrentLogProfile"/>.
    /// </summary>
    public static readonly StyledProperty<LogProfile?> CurrentLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, LogProfile?>(nameof(CurrentLogProfile));
    /// <summary>
    /// Property of <see cref="EnableActionsOnCurrentLogProfile"/>.
    /// </summary>
    public static readonly StyledProperty<bool> EnableActionsOnCurrentLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>(nameof(EnableActionsOnCurrentLogProfile), true);
    /// <summary>
    /// Property of <see cref="ShowEmptyLogProfile"/>.
    /// </summary>
    public static readonly StyledProperty<bool> ShowEmptyLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>(nameof(ShowEmptyLogProfile), false);
    
    
    // Adapter of weak event handler.
    class WeakProductActivationStateChangedHandler : IDisposable
    {
        // Fields.
        readonly WeakReference<Action<IProductManager, string, bool>> handlerRef;
        int isDisposed;
        readonly SynchronizationContext? syncContext;
        readonly IProductManager target;

        // Constructor.
        public WeakProductActivationStateChangedHandler(IProductManager target, Action<IProductManager, string, bool> handler)
        {
            this.handlerRef = new(handler);
            this.syncContext = SynchronizationContext.Current;
            this.target = target;
            target.ProductActivationChanged += this.OnProductActivationStateChanged;
        }

        // Dispose.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
                return;
            if (this.syncContext != null && this.syncContext != SynchronizationContext.Current)
            {
                try
                {
                    this.syncContext.Post(_ => this.target.ProductActivationChanged -= this.OnProductActivationStateChanged, null);
                    return;
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch
                { }
                // ReSharper restore EmptyGeneralCatchClause
            }
            this.target.ProductActivationChanged -= this.OnProductActivationStateChanged;
        }

        // Entry of event handler.
        void OnProductActivationStateChanged(IProductManager productManager, string productId, bool isActivated)
        {
            if (this.handlerRef.TryGetTarget(out var handler))
                handler(productManager, productId, isActivated);
            else
                this.Dispose();
        }
    }

    
    // Constants.
    const int ActionsOnCurrentLogProfileTag = 10;
    const int CopyCurrentLogProfileTag = 1;
    const int EditCurrentLogProfileTag = 0;
    const int ExportCurrentLogProfileTag = 2;
    const int PinnedLogProfilesTag = 11;
    const int RecentlyUsedLogProfilesTag = 12;
    const int RemoveCurrentLogProfileTag = 3;


    // Static fields.
    static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>("IsProVersionActivated", false);


    // Fields.
    Separator? actionsOnCurrentLogProfileSeparator;
    MenuItem? copyCurrentLogProfileMenuItem;
    MenuItem? editCurrentLogProfileMenuItem;
    private MenuItem? emptyLogProfileMenuItem;
    MenuItem? exportCurrentLogProfileMenuItem;
    bool isReady;
    readonly SortedObservableList<object> items;
    readonly LogProfileManager logProfileManager = LogProfileManager.Default;
    readonly PropertyChangedEventHandler logProfilePropertyChangedHandler;
    readonly Dictionary<LogProfile, IDisposable> logProfilePropertyChangedHandlerTokens = new();
    readonly NotifyCollectionChangedEventHandler logProfilesChangedHandler;
    IDisposable? logProfilesChangedHandlerToken;
    readonly HashSet<LogProfile> pinnedLogProfiles = new();
    readonly Separator pinnedLogProfilesSeparator = new()
    {
        Tag = PinnedLogProfilesTag,
    };
    readonly Action<IProductManager, string, bool> productActivationChangedHandler;
    IDisposable? productActivationChangedHandlerToken;
    readonly HashSet<LogProfile> recentlyUsedLogProfiles = new(); // Excluding pinned log profiles
    readonly NotifyCollectionChangedEventHandler recentlyUsedLogProfilesChangedHandler;
    IDisposable? recentlyUsedLogProfilesChangedHandlerToken;
    readonly Separator recentlyUsedLogProfilesSeparator = new()
    {
        Tag = RecentlyUsedLogProfilesTag,
    };
    MenuItem? removeCurrentLogProfileMenuItem;


    /// <summary>
    /// Initialize new <see cref="LogProfileSelectionContextMenu"/> instance.
    /// </summary>
    public LogProfileSelectionContextMenu()
    {
        this.items = new(this.CompareItems);
        base.ItemsSource = ListExtensions.AsReadOnly(this.items);
        this.logProfilesChangedHandler = this.OnLogProfilesChanged;
        this.logProfilePropertyChangedHandler = this.OnLogProfilePropertyChanged;
        this.productActivationChangedHandler = this.OnProductActivationChanged;
        this.recentlyUsedLogProfilesChangedHandler = this.OnRecentlyUsedLogProfilesChanged;
        Grid.SetIsSharedSizeScope(this, true);
        this.Closed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (this.items.IsNotEmpty())
                    this.ScrollIntoView(0);
            }, Avalonia.Threading.DispatcherPriority.Normal);
        };
        this.GetObservable(CurrentLogProfileProperty).Subscribe(currentLogProfile =>
        {
            this.UpdateActionOnCurrentLogProfileMenuItemsVisibility();
            this.UpdateActionOnCurrentLogProfileMenuItemStates();
            foreach (var item in this.items)
            {
                if (item is not MenuItem menuItem || menuItem.Header is not Panel panel)
                    continue;
                var isCurrentLogProfile = currentLogProfile != null && ReferenceEquals(menuItem.DataContext, currentLogProfile);
                (panel.Children[0] as Avalonia.Controls.TextBlock)?.Let(it =>
                    it.FontWeight = isCurrentLogProfile ? FontWeight.Bold : FontWeight.Normal);
                panel.Children[2].Let(it =>
                    it.IsVisible = isCurrentLogProfile);
            }
        });
        this.GetObservable(EnableActionsOnCurrentLogProfileProperty).Subscribe(_ =>
        {
            this.UpdateActionOnCurrentLogProfileMenuItemsVisibility();
        });
        this.GetObservable(IsProVersionActivatedProperty).Subscribe(_ =>
        {
            this.UpdateActionOnCurrentLogProfileMenuItemStates();
        });
        this.GetObservable(ShowEmptyLogProfileProperty).Subscribe(show =>
        {
            if (!this.isReady)
                return;
            if (show)
            {
                this.emptyLogProfileMenuItem ??= this.CreateMenuItem(this.logProfileManager.EmptyProfile);
                this.items.Add(this.emptyLogProfileMenuItem);
            }
            else if (this.emptyLogProfileMenuItem is not null)
                this.items.Remove(this.emptyLogProfileMenuItem);
        });
    }
    
    
    // Finalizer.
    ~LogProfileSelectionContextMenu()
    {
        if (!this.isReady)
            return;
        this.logProfilesChangedHandlerToken?.Dispose();
        this.productActivationChangedHandlerToken?.Dispose();
        this.recentlyUsedLogProfilesChangedHandlerToken?.Dispose();
        foreach (var token in this.logProfilePropertyChangedHandlerTokens.Values)
            token.Dispose();
    }


    // Compare menu items.
    int CompareItems(object? lhs, object? rhs)
    {
        var lTag = ((lhs as Control)?.Tag as int?) ?? -1;
        var rTag = ((rhs as Control)?.Tag as int?) ?? -1;
        if (lTag >= 0 && rTag >= 0)
            return lTag - rTag;
        LogProfile lhsLogProfile;
        LogProfile rhsLogProfile;
        if (lhs is Separator)
        {
            rhsLogProfile = (LogProfile)((MenuItem)rhs!).DataContext!;
            if (rhsLogProfile.IsPinned)
            {
                if (lTag <= ActionsOnCurrentLogProfileTag)
                    return -1;
                return 1;
            }
            if (this.recentlyUsedLogProfiles.Contains(rhsLogProfile))
            {
                if (lTag <= PinnedLogProfilesTag)
                    return -1;
                return 1;
            }
            return -1;
        }
        if (rhs is Separator)
        {
            lhsLogProfile = (LogProfile)((MenuItem)lhs!).DataContext!;
            if (lhsLogProfile.IsPinned)
            {
                if (rTag <= ActionsOnCurrentLogProfileTag)
                    return 1;
                return -1;
            }
            if (this.recentlyUsedLogProfiles.Contains(lhsLogProfile))
            {
                if (rTag <= PinnedLogProfilesTag)
                    return 1;
                return -1;
            }
            return 1;
        }
        if (lTag >= 0)
            return -1;
        if (rTag >= 0)
            return 1;
        lhsLogProfile = (LogProfile)((MenuItem)lhs!).DataContext!;
        rhsLogProfile = (LogProfile)((MenuItem)rhs!).DataContext!;
        if (lhsLogProfile.IsPinned)
        {
            if (rhsLogProfile.IsPinned)
                return CompareLogProfiles(lhsLogProfile, rhsLogProfile);
            return -1;
        }
        if (rhsLogProfile.IsPinned)
            return 1;
        if (this.recentlyUsedLogProfiles.Contains(lhsLogProfile))
        {
            var lhsRsLogProfileIndex = this.logProfileManager.RecentlyUsedProfiles.IndexOf(lhsLogProfile);
            if (this.recentlyUsedLogProfiles.Contains(rhsLogProfile))
                return lhsRsLogProfileIndex - this.logProfileManager.RecentlyUsedProfiles.IndexOf(rhsLogProfile);
            return -1;
        }
        if (this.recentlyUsedLogProfiles.Contains(rhsLogProfile))
            return 1;
        return this.CompareLogProfiles(lhsLogProfile, rhsLogProfile);
    }


    // Compare log profiles.
    int CompareLogProfiles(LogProfile lhs, LogProfile rhs)
    {
        var logProfileManager = this.logProfileManager;
        if (lhs == logProfileManager.EmptyProfile)
            return -1;
        if (rhs == logProfileManager.EmptyProfile)
            return 1;
        var result = string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture);
        if (result != 0)
            return result;
        return string.CompareOrdinal(lhs.Id, rhs.Id);
    }


    // Copy current log profile.
    async void CopyCurrentLogProfile()
    {
        // get state
        var logProfile = this.CurrentLogProfile;
        var window = this.FindLogicalAncestorOfType<CarinaStudio.Controls.Window>();
        var app = App.CurrentOrNull;
        if (logProfile == null || window == null || app == null)
            return;
        
        // copy log profile
        var newProfile = await new LogProfileEditorDialog()
        {
            LogProfile = new LogProfile(logProfile)
            {
                Name = Utility.GenerateName(logProfile.Name, name =>
                    LogProfileManager.Default.Profiles.FirstOrDefault(it => it.Name == name) != null),
            },
        }.ShowDialog<LogProfile?>(window);
        if (newProfile is null || window.IsClosed)
            return;
        LogProfileManager.Default.AddProfile(newProfile);
        this.LogProfileCreated?.Invoke(this, newProfile);
    }


    /// <summary>
    /// Get or set current log profile.
    /// </summary>
    public LogProfile? CurrentLogProfile
    {
        get => this.GetValue(CurrentLogProfileProperty);
        set => this.SetValue(CurrentLogProfileProperty, value);
    }


    // Create menu item for copying current log profile.
    MenuItem CreateCopyCurrentLogProfileMenuItem() => new MenuItem().Also(menuItem =>
    {
        menuItem.Command = new CarinaStudio.Windows.Input.Command(this.CopyCurrentLogProfile);
        menuItem.Header = new FormattedTextBlock().Also(it =>
        {
            it.Bind(FormattedTextBlock.Arg1Property, new Binding
            {
                Path = $"{nameof(CurrentLogProfile)}.{nameof(LogProfile.Name)}",
                Source = this,
            });
            it.Bind(FormattedTextBlock.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionContextMenu.CopyCurrentLogProfile"));
        });
        menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Source = this.FindResourceOrDefault<IImage>("Image/Icon.Copy.Outline");
        });
        menuItem.Tag = CopyCurrentLogProfileTag;
    });


    // Create menu item for editing current log profile.
    MenuItem CreateEditCurrentLogProfileMenuItem() => new MenuItem().Also(menuItem =>
    {
        menuItem.Command = new CarinaStudio.Windows.Input.Command(this.EditCurrentLogProfile);
        menuItem.Header = new FormattedTextBlock().Also(it =>
        {
            it.Bind(FormattedTextBlock.Arg1Property, new Binding
            {
                Path = $"{nameof(CurrentLogProfile)}.{nameof(LogProfile.Name)}",
                Source = this,
            });
            it.Bind(FormattedTextBlock.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionContextMenu.EditCurrentLogProfile"));
        });
        menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Source = this.FindResourceOrDefault<IImage>("Image/Icon.Edit.Outline");
        });
        menuItem.Tag = EditCurrentLogProfileTag;
    });


    // Create menu item for exporting current log profile.
    MenuItem CreateExportCurrentLogProfileMenuItem() => new MenuItem().Also(menuItem =>
    {
        menuItem.Command = new CarinaStudio.Windows.Input.Command(this.ExportCurrentLogProfile);
        menuItem.Header = new FormattedTextBlock().Also(it =>
        {
            it.Bind(FormattedTextBlock.Arg1Property, new Binding
            {
                Path = $"{nameof(CurrentLogProfile)}.{nameof(LogProfile.Name)}",
                Source = this,
            });
            it.Bind(FormattedTextBlock.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionContextMenu.ExportCurrentLogProfile"));
        });
        menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Source = this.FindResourceOrDefault<IImage>("Image/Icon.Export");
        });
        menuItem.Tag = ExportCurrentLogProfileTag;
    });


    // Create menu item for log profile.
    MenuItem CreateMenuItem(LogProfile logProfile) => new MenuItem().Also(menuItem =>
    {
        menuItem.Click += (_, _) =>
        {
            this.Close();
            this.LogProfileSelected?.Invoke(this, logProfile);
        };
        menuItem.DataContext = logProfile;
        menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Bind(Avalonia.Controls.Image.SourceProperty, new MultiBinding
            {
                Bindings = 
                {
                    new Binding { Path = nameof(LogProfile.Icon) },
                    new Binding { Path = nameof(LogProfile.IconColor) }
                },
                Converter = LogProfileIconConverter.Default,
            });
        });
        menuItem.Header = new Grid().Also(grid =>
        {
            grid.ColumnDefinitions.Add(new(1, GridUnitType.Star)
            {
                SharedSizeGroup = "Name",
            });
            grid.ColumnDefinitions.Add(new(1, GridUnitType.Auto));
            grid.ColumnDefinitions.Add(new(1, GridUnitType.Auto));
            var nameTextBlock = new Avalonia.Controls.TextBlock().Also(it =>
            {
                it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding() 
                { 
                    Path = nameof(LogProfile.Name) 
                });
                it.TextTrimming = TextTrimming.CharacterEllipsis;
                it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            });
            var currentLogProfileTextBlock = new Avalonia.Controls.TextBlock().Also(it =>
            {
                it.Opacity = this.FindResourceOrDefault<double>("Double/LogProfileSelectionContextMenu.CurrentLogProfile.Opacity");
                it.Bind(Avalonia.Controls.TextBlock.TextProperty, this.GetResourceObservable("String/LogProfileSelectionContextMenu.CurrentLogProfile"));
                it.TextTrimming = TextTrimming.CharacterEllipsis;
                it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                Grid.SetColumn(it, 2);
            });
            grid.Children.Add(nameTextBlock);
            grid.Children.Add(new Separator().Also(it =>
            {
                it.Classes.Add("Dialog_Separator_Small");
                it.Bind(IsVisibleProperty, new Binding
                {
                    Path = nameof(IsVisible),
                    Source = currentLogProfileTextBlock,
                });
                Grid.SetColumn(it, 1);
            }));
            grid.Children.Add(currentLogProfileTextBlock);
            menuItem.GetObservable(DataContextProperty).Subscribe(dataContext =>
            {
                var currentLogProfile = this.CurrentLogProfile;
                if (currentLogProfile is null || !ReferenceEquals(dataContext, currentLogProfile))
                {
                    nameTextBlock.FontWeight = FontWeight.Normal;
                    currentLogProfileTextBlock.IsVisible = false;
                }
                else
                {
                    nameTextBlock.FontWeight = FontWeight.Bold;
                    currentLogProfileTextBlock.IsVisible = true;
                }
            });
        });
        menuItem.Bind(IsEnabledProperty, new MultiBinding().Also(it =>
        {
            it.Bindings.Add(new Binding
            {
                Path = "IsProVersionActivated",
                Source = this,
            });
            it.Bindings.Add(new Binding
            {
                Path = $"!{nameof(LogProfile.DataSourceProvider)}.{nameof(Logs.DataSources.ILogDataSourceProvider.IsProVersionOnly)}",
            });
            it.Converter = Avalonia.Data.Converters.BoolConverters.Or;
        }));
    });


    // Create menu item for removing current log profile.
    MenuItem CreateRemoveCurrentLogProfileMenuItem() => new MenuItem().Also(menuItem =>
    {
        menuItem.Command = new CarinaStudio.Windows.Input.Command(this.RemoveCurrentLogProfile);
        menuItem.Header = new FormattedTextBlock().Also(it =>
        {
            it.Bind(FormattedTextBlock.Arg1Property, new Binding
            {
                Path = $"{nameof(CurrentLogProfile)}.{nameof(LogProfile.Name)}",
                Source = this,
            });
            it.Bind(FormattedTextBlock.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionContextMenu.RemoveCurrentLogProfile"));
        });
        menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Source = this.FindResourceOrDefault<IImage>("Image/Icon.Delete.Outline");
        });
        menuItem.Tag = RemoveCurrentLogProfileTag;
    });


    // Edit current log profile.
    void EditCurrentLogProfile()
    {
        // get state
        var logProfile = this.CurrentLogProfile;
        var window = this.FindLogicalAncestorOfType<CarinaStudio.Controls.Window>();
        var app = App.CurrentOrNull;
        if (logProfile == null || window == null || app == null || logProfile.IsBuiltIn)
            return;
        
        // edit log profile
        LogProfileEditorDialog.Show(window, logProfile);
    }


    /// <summary>
    /// Get or set whether item of editing current log profile is visible or not.
    /// </summary>
    public bool EnableActionsOnCurrentLogProfile
    {
        get => this.GetValue(EnableActionsOnCurrentLogProfileProperty);
        set => this.SetValue(EnableActionsOnCurrentLogProfileProperty, value);
    }


    // Export current log profile.
    async void ExportCurrentLogProfile()
    {
        // check state
        var logProfile = this.CurrentLogProfile;
        var window = this.FindLogicalAncestorOfType<CarinaStudio.Controls.Window>();
        if (logProfile is null || window is null)
            return;
        
        // select a file
        var fileName = await FileSystemItemSelection.SelectFileToExportLogProfileAsync(window);
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // copy and export log profile
        var copiedProfile = new LogProfile(logProfile);
        try
        {
            await copiedProfile.SaveAsync(fileName, false);
        }
        catch
        {
            if (this.LogProfileExportingFailed?.Invoke(this, logProfile, fileName) != true)
            {
                _ = new MessageDialog
                {
                    Icon = MessageDialogIcon.Error,
                    Message = new FormattedString().Also(it =>
                    {
                        it.Arg1 = fileName;
                        it.BindToResource(FormattedString.FormatProperty, window, "String/LogProfileSelectionDialog.FailedToExportLogProfile");
                    }),
                }.ShowDialog(window);
            }
            return;
        }
        
        // complete
        if (this.LogProfileExported?.Invoke(this, logProfile, fileName) == true)
            return;
        _ = new MessageDialog
        {
            Icon = MessageDialogIcon.Success,
            Message = new FormattedString().Also(it =>
            {
                it.Arg1 = fileName;
                it.BindToResource(FormattedString.FormatProperty, window, "String/LogProfileSelectionDialog.LogProfileExported");
            }),
        }.ShowDialog(window);
    }


    /// <summary>
    /// Get items of menu.
    /// </summary>
    public new object? ItemsSource => base.ItemsSource;


    /// <summary>
    /// Raised when new log profile was just created.
    /// </summary>
    public event Action<LogProfileSelectionContextMenu, LogProfile>? LogProfileCreated;
    
    
    /// <summary>
    /// Raised when log profile has been exported.
    /// </summary>
    public event Func<LogProfileSelectionContextMenu, LogProfile, string, bool>? LogProfileExported; 


    /// <summary>
    /// Raised when failed to export log profile.
    /// </summary>
    public event Func<LogProfileSelectionContextMenu, LogProfile, string, bool>? LogProfileExportingFailed; 


    /// <summary>
    /// Raised when specific log profile has been removed.
    /// </summary>
    public event Action<LogProfileSelectionContextMenu, LogProfile>? LogProfileRemoved;


    /// <summary>
    /// Raised when user selected a log profile.
    /// </summary>
    public event Action<LogProfileSelectionContextMenu, LogProfile>? LogProfileSelected;


    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (this.isReady)
            return;
        this.logProfilesChangedHandlerToken = (LogProfileManager.Default.Profiles as INotifyCollectionChanged)?.Let(it =>
            it.AddWeakCollectionChangedEventHandler(this.logProfilesChangedHandler));
        this.recentlyUsedLogProfilesChangedHandlerToken = (LogProfileManager.Default.RecentlyUsedProfiles as INotifyCollectionChanged)?.Let(it =>
            it.AddWeakCollectionChangedEventHandler(this.recentlyUsedLogProfilesChangedHandler));
        this.items.AddAll(new List<object>().Also(it =>
        {
            if (this.CurrentLogProfile != null && this.EnableActionsOnCurrentLogProfile)
            {
                this.copyCurrentLogProfileMenuItem ??= this.CreateCopyCurrentLogProfileMenuItem();
                this.editCurrentLogProfileMenuItem ??= this.CreateEditCurrentLogProfileMenuItem();
                this.exportCurrentLogProfileMenuItem ??= this.CreateExportCurrentLogProfileMenuItem();
                this.removeCurrentLogProfileMenuItem ??= this.CreateRemoveCurrentLogProfileMenuItem();
                this.actionsOnCurrentLogProfileSeparator ??= new()
                {
                    Tag = ActionsOnCurrentLogProfileTag,
                };
                it.Add(this.editCurrentLogProfileMenuItem);
                it.Add(this.copyCurrentLogProfileMenuItem);
                it.Add(this.exportCurrentLogProfileMenuItem);
                it.Add(this.removeCurrentLogProfileMenuItem);
                it.Add(this.actionsOnCurrentLogProfileSeparator);
                this.UpdateActionOnCurrentLogProfileMenuItemStates();
            }
            var recentlyUsedLogProfiles = LogProfileManager.Default.RecentlyUsedProfiles;
            foreach (var logProfile in LogProfileManager.Default.Profiles)
            {
                this.logProfilePropertyChangedHandlerTokens[logProfile] = logProfile.AddWeakPropertyChangedEventHandler(this.logProfilePropertyChangedHandler);
                if (logProfile.IsTemplate)
                    continue;
                if (logProfile.IsPinned)
                    this.pinnedLogProfiles.Add(logProfile);
                else if (recentlyUsedLogProfiles.Contains(logProfile))
                    this.recentlyUsedLogProfiles.Add(logProfile);
                it.Add(this.CreateMenuItem(logProfile));
            }
            if (this.pinnedLogProfiles.IsNotEmpty())
                it.Add(this.pinnedLogProfilesSeparator);
            if (this.recentlyUsedLogProfiles.IsNotEmpty())
                it.Add(this.recentlyUsedLogProfilesSeparator);
        }));
        App.Current.ProductManager.Let(it =>
        {
            if (it.IsMock)
                this.SetValue(IsProVersionActivatedProperty, false);
            else
            {
                this.SetValue(IsProVersionActivatedProperty, it.IsProductActivated(Products.Professional));
                this.productActivationChangedHandlerToken = new WeakProductActivationStateChangedHandler(it, this.productActivationChangedHandler);
            }
        });
        if (this.GetValue(ShowEmptyLogProfileProperty))
        {
            this.emptyLogProfileMenuItem = this.CreateMenuItem(this.logProfileManager.EmptyProfile);
            this.items.Add(this.emptyLogProfileMenuItem);
        }
        this.isReady = true;
    }


    // Called when property of log profile changed.
    void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LogProfile logProfile)
            return;
        MenuItem? menuItem;
        switch (e.PropertyName)
        {
            case nameof(LogProfile.DataSourceProvider):
                this.UpdateActionOnCurrentLogProfileMenuItemStates();
                break;
            case nameof(LogProfile.IsPinned):
                if (this.TryFindMenuItem(logProfile, out menuItem))
                    this.items.Sort(menuItem);
                if (logProfile.IsPinned)
                {
                    if (this.pinnedLogProfiles.Add(logProfile)
                        && this.pinnedLogProfiles.Count == 1)
                    {
                        this.items.Add(this.pinnedLogProfilesSeparator);
                    }
                    if (this.recentlyUsedLogProfiles.Remove(logProfile)
                        && this.recentlyUsedLogProfiles.IsEmpty())
                    {
                        this.items.Remove(this.recentlyUsedLogProfilesSeparator);
                    }
                }
                else
                {
                    if (this.pinnedLogProfiles.Remove(logProfile)
                        && this.pinnedLogProfiles.IsEmpty())
                    {
                        this.items.Remove(this.pinnedLogProfilesSeparator);
                    }
                    if (LogProfileManager.Default.RecentlyUsedProfiles.Contains(logProfile)
                        && this.recentlyUsedLogProfiles.Add(logProfile))
                    {
                        if (this.recentlyUsedLogProfiles.Count == 1)
                            this.items.Add(this.recentlyUsedLogProfilesSeparator);
                        if (menuItem is not null)
                            this.items.Sort(menuItem);
                    }
                }
                break;
            case nameof(LogProfile.IsTemplate):
                if (logProfile.IsTemplate)
                {
                    this.items.RemoveAll(it =>
                        it is MenuItem menuItem && menuItem.DataContext == logProfile);
                    if (this.pinnedLogProfiles.Remove(logProfile)
                        && this.pinnedLogProfiles.IsEmpty())
                    {
                        this.items.Remove(this.pinnedLogProfilesSeparator);
                    }
                    if (this.recentlyUsedLogProfiles.Remove(logProfile)
                        && this.recentlyUsedLogProfiles.IsEmpty())
                    {
                        this.items.Remove(this.recentlyUsedLogProfilesSeparator);
                    }
                }
                else
                {
                    this.items.Add(this.CreateMenuItem(logProfile));
                    if (logProfile.IsPinned)
                    {
                        if (this.pinnedLogProfiles.Add(logProfile)
                            && this.pinnedLogProfiles.Count == 1)
                        {
                            this.items.Add(this.pinnedLogProfilesSeparator);
                        }
                    }
                    else
                    {
                        if (LogProfileManager.Default.RecentlyUsedProfiles.Contains(logProfile)
                            && this.recentlyUsedLogProfiles.Add(logProfile)
                            && this.recentlyUsedLogProfiles.Count == 1)
                        {
                            this.items.Add(this.recentlyUsedLogProfilesSeparator);
                        }
                    }
                }
                break;
            case nameof(LogProfile.Name):
                if (this.TryFindMenuItem(logProfile, out menuItem))
                    this.items.Sort(menuItem);
                break;
        }
    }


    // Called when list of all log profiles changed.
    void OnLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var isPinnedLogProfileSeparatorShown = this.pinnedLogProfiles.IsNotEmpty();
        var isRecentlyUsedLogProfilesSeparatorShown = this.recentlyUsedLogProfiles.IsNotEmpty();
        var recentlyUsedLogProfiles = LogProfileManager.Default.RecentlyUsedProfiles;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var logProfile in e.NewItems!.Cast<LogProfile>())
                {
                    this.logProfilePropertyChangedHandlerTokens[logProfile] = logProfile.AddWeakPropertyChangedEventHandler(this.logProfilePropertyChangedHandler);
                    if (logProfile.IsTemplate)
                        continue;
                    if (logProfile.IsPinned)
                        this.pinnedLogProfiles.Add(logProfile);
                    else if (recentlyUsedLogProfiles.Contains(logProfile))
                        this.recentlyUsedLogProfiles.Add(logProfile);
                    this.items.Add(this.CreateMenuItem(logProfile));
                }
                if (!isPinnedLogProfileSeparatorShown
                    && this.pinnedLogProfiles.IsNotEmpty())
                {
                    this.items.Add(this.pinnedLogProfilesSeparator);
                }
                if (!isRecentlyUsedLogProfilesSeparatorShown && this.recentlyUsedLogProfiles.IsNotEmpty())
                    this.items.Add(this.recentlyUsedLogProfilesSeparator);
                break;
            case NotifyCollectionChangedAction.Move:
                foreach (var logProfile in e.NewItems!.Cast<LogProfile>())
                {
                    if (logProfile.IsTemplate)
                        continue;
                    if (this.TryFindMenuItem(logProfile, out var item))
                        this.items.Sort(item);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var logProfile in e.OldItems!.Cast<LogProfile>())
                {
                    if (this.logProfilePropertyChangedHandlerTokens.TryGetValue(logProfile, out var token))
                    {
                        token.Dispose();
                        this.logProfilePropertyChangedHandlerTokens.Remove(logProfile);
                    }
                    this.items.RemoveAll(it => it is MenuItem menuItem && menuItem.DataContext == logProfile);
                    this.pinnedLogProfiles.Remove(logProfile);
                    this.recentlyUsedLogProfiles.Remove(logProfile);
                }
                if (isPinnedLogProfileSeparatorShown && this.pinnedLogProfiles.IsEmpty())
                    this.items.Remove(this.pinnedLogProfilesSeparator);
                if (isRecentlyUsedLogProfilesSeparatorShown && this.recentlyUsedLogProfiles.IsEmpty())
                    this.items.Remove(this.recentlyUsedLogProfilesSeparator);
                break;
            default:
                throw new NotSupportedException($"Unsupported action of change of log profile list: {e.Action}");
        }
    }


    // Called when activation state of product changed.
    void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
    {
        if (productId == Products.Professional)
            this.SetValue(IsProVersionActivatedProperty, isActivated);
    }


     // Called when list of recently used log profiles changed.
    void OnRecentlyUsedLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var isRecentlyUsedLogProfilesSeparatorShown = this.recentlyUsedLogProfiles.IsNotEmpty();
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var logProfile in e.NewItems!.Cast<LogProfile>())
                {
                    if (logProfile.IsTemplate || logProfile.IsPinned)
                        continue;
                    if (this.recentlyUsedLogProfiles.Add(logProfile)
                        && this.TryFindMenuItem(logProfile, out var menuItem))
                    {
                        this.items.Sort(menuItem);
                    }
                }
                if (!isRecentlyUsedLogProfilesSeparatorShown && this.recentlyUsedLogProfiles.IsNotEmpty())
                    this.items.Add(this.recentlyUsedLogProfilesSeparator);
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var logProfile in e.OldItems!.Cast<LogProfile>())
                {
                    if (this.recentlyUsedLogProfiles.Remove(logProfile)
                        && this.TryFindMenuItem(logProfile, out var menuItem))
                    {
                        this.items.Sort(menuItem);
                    }
                }
                if (isRecentlyUsedLogProfilesSeparatorShown && this.recentlyUsedLogProfiles.IsEmpty())
                    this.items.Remove(this.recentlyUsedLogProfilesSeparator);
                break;
            case NotifyCollectionChangedAction.Reset:
                var recentlyUsedLogProfiles = LogProfileManager.Default.RecentlyUsedProfiles;
                foreach (var logProfile in this.recentlyUsedLogProfiles.ToArray())
                {
                    if (recentlyUsedLogProfiles.Contains(logProfile) && !logProfile.IsTemplate && !logProfile.IsPinned)
                        continue;
                    this.recentlyUsedLogProfiles.Remove(logProfile);
                    if (this.TryFindMenuItem(logProfile, out var menuItem))
                        this.items.Sort(menuItem);
                }
                foreach (var logProfile in LogProfileManager.Default.Profiles)
                {
                    if (!logProfile.IsTemplate && !logProfile.IsPinned && recentlyUsedLogProfiles.Contains(logProfile))
                        this.recentlyUsedLogProfiles.Add(logProfile);
                    if (this.TryFindMenuItem(logProfile, out var menuItem))
                        this.items.Sort(menuItem);
                }
                if (this.recentlyUsedLogProfiles.IsEmpty())
                    this.items.Remove(this.recentlyUsedLogProfilesSeparator);
                else if (!isRecentlyUsedLogProfilesSeparatorShown)
                    this.items.Add(this.recentlyUsedLogProfilesSeparator);
                break;
            default:
                throw new NotSupportedException();
        }
    }


    // Remove current log profile.
    async void RemoveCurrentLogProfile()
    {
        // get state
        var logProfile = this.CurrentLogProfile;
        var window = this.FindLogicalAncestorOfType<CarinaStudio.Controls.Window>();
        var app = App.CurrentOrNull;
        if (logProfile == null || window == null || app == null || logProfile.IsBuiltIn)
            return;
        
        // confirm
        var result = await new MessageDialog()
        {
            Buttons = MessageDialogButtons.YesNo,
            DefaultResult = MessageDialogResult.No,
            Icon = MessageDialogIcon.Question,
            Message = new FormattedString().Also(it =>
            {
                it.Arg1 = logProfile.Name;
                it.Bind(FormattedString.FormatProperty, app.GetObservableString("LogProfileSelectionDialog.ConfirmRemovingLogProfile"));
            }),
        }.ShowDialog(window);
        if (result == MessageDialogResult.No)
            return;
        
        // remove log profile
        LogProfileManager.Default.RemoveProfile(logProfile);
        this.LogProfileRemoved?.Invoke(this, logProfile);
    }


    /// <summary>
    /// Get or set whether empty log profile should be shown or not.
    /// </summary>
    public bool ShowEmptyLogProfile
    {
        get => this.GetValue(ShowEmptyLogProfileProperty);
        set => this.SetValue(ShowEmptyLogProfileProperty, value);
    }
    
    
    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ContextMenu);


    // Try find menu item for given log profile.
    bool TryFindMenuItem(LogProfile logProfile, [NotNullWhen(true)] out MenuItem? menuItem)
    {
        menuItem = this.items.FirstOrDefault(it => it is MenuItem menuItem && menuItem.DataContext == logProfile) as MenuItem;
        return menuItem != null;
    }
    
    
    // Update state of log action items.
    void UpdateActionOnCurrentLogProfileMenuItemStates()
    {
        var profile = this.CurrentLogProfile;
        var isValidProfile = profile is not null && (!profile.DataSourceProvider.IsProVersionOnly || this.GetValue(IsProVersionActivatedProperty));
        var isBuiltInProfile = profile?.IsBuiltIn == true;
        this.copyCurrentLogProfileMenuItem?.Let(it => it.IsEnabled = isValidProfile);
        this.editCurrentLogProfileMenuItem?.Let(it => it.IsEnabled = isValidProfile && !isBuiltInProfile);
        this.exportCurrentLogProfileMenuItem?.Let(it => it.IsEnabled = isValidProfile);
        this.removeCurrentLogProfileMenuItem?.Let(it => it.IsEnabled = isValidProfile && !isBuiltInProfile);
    }


    // Update visibility of actions on current log profile.
    void UpdateActionOnCurrentLogProfileMenuItemsVisibility()
    {
        if (this.isReady)
        {
            if (this.CurrentLogProfile != null && this.EnableActionsOnCurrentLogProfile)
            {
                this.copyCurrentLogProfileMenuItem ??= this.CreateCopyCurrentLogProfileMenuItem();
                this.editCurrentLogProfileMenuItem ??= this.CreateEditCurrentLogProfileMenuItem();
                this.exportCurrentLogProfileMenuItem ??= this.CreateExportCurrentLogProfileMenuItem();
                this.removeCurrentLogProfileMenuItem ??= this.CreateRemoveCurrentLogProfileMenuItem();
                this.actionsOnCurrentLogProfileSeparator ??= new()
                {
                    Tag = ActionsOnCurrentLogProfileTag,
                };
                if (!this.items.Contains(this.actionsOnCurrentLogProfileSeparator))
                {
                    this.items.Add(this.editCurrentLogProfileMenuItem);
                    this.items.Add(this.copyCurrentLogProfileMenuItem);
                    this.items.Add(this.exportCurrentLogProfileMenuItem);
                    this.items.Add(this.removeCurrentLogProfileMenuItem);
                    this.items.Add(this.actionsOnCurrentLogProfileSeparator);
                    this.UpdateActionOnCurrentLogProfileMenuItemStates();
                }
            }
            else
            {
                if (this.copyCurrentLogProfileMenuItem != null)
                    this.items.Remove(this.copyCurrentLogProfileMenuItem);
                if (this.editCurrentLogProfileMenuItem != null)
                    this.items.Remove(this.editCurrentLogProfileMenuItem);
                if (this.exportCurrentLogProfileMenuItem != null)
                    this.items.Remove(this.exportCurrentLogProfileMenuItem);
                if (this.removeCurrentLogProfileMenuItem != null)
                    this.items.Remove(this.removeCurrentLogProfileMenuItem);
                if (this.actionsOnCurrentLogProfileSeparator != null)
                    this.items.Remove(this.actionsOnCurrentLogProfileSeparator);
            }
        }
    }
}