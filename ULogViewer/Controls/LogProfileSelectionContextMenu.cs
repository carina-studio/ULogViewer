using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
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

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ContextMenu"/> to select log profile.
/// </summary>
class LogProfileSelectionContextMenu : ContextMenu, IStyleable
{
    /// <summary>
    /// Property of <see cref="CurrentLogProfile"/>.
    /// </summary>
    public static readonly StyledProperty<LogProfile?> CurrentLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, LogProfile?>(nameof(CurrentLogProfile));
    /// <summary>
    /// Property of <see cref="EnableActionsOnCurrentLogProfile"/>.
    /// </summary>
    public static readonly StyledProperty<bool> EnableActionsOnCurrentLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>(nameof(EnableActionsOnCurrentLogProfile), true);

    
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
    MenuItem? exportCurrentLogProfileMenuItem;
    bool isAttachedToLogicalTree;
    readonly SortedObservableList<object> items;
    readonly LogProfileManager logProfileManager = LogProfileManager.Default;
    readonly HashSet<LogProfile> pinnedLogProfiles = new();
    readonly Separator pinnedLogProfilesSeparator = new()
    {
        Tag = PinnedLogProfilesTag,
    };
    readonly HashSet<LogProfile> recentlyUsedLogProfiles = new(); // Excluding pinned log profiles
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
        base.Items = this.items;
        this.Items = ListExtensions.AsReadOnly(this.items);
        Grid.SetIsSharedSizeScope(this, true);
        this.MenuClosed += (_, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (this.items.IsNotEmpty())
                    this.ScrollIntoView(0);
            }, Avalonia.Threading.DispatcherPriority.Normal);
        };
        this.GetObservable(EnableActionsOnCurrentLogProfileProperty).Subscribe(_ =>
        {
            this.UpdateActionOnCurrentLogProfileMenuItemsVisibility();
        });
        this.GetObservable(CurrentLogProfileProperty).Subscribe(currentLogProfile =>
        {
            this.UpdateActionOnCurrentLogProfileMenuItemsVisibility();
            foreach (var item in this.items)
            {
                if (item is not MenuItem menuItem || menuItem.Header is not Panel panel)
                    continue;
                var isCurrentLogProfile = currentLogProfile != null && menuItem.DataContext == currentLogProfile;
                (panel.Children[0] as Avalonia.Controls.TextBlock)?.Let(it =>
                    it.FontWeight = isCurrentLogProfile ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal);
                (panel.Children[2] as Control)?.Let(it =>
                    it.IsVisible = isCurrentLogProfile);
            }
        });
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
        else if (rhsLogProfile.IsPinned)
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
        return CompareLogProfiles(lhsLogProfile, rhsLogProfile);
    }


    // Compare log profiles.
    static int CompareLogProfiles(LogProfile lhs, LogProfile rhs)
    {
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
        }.ShowDialog<LogProfile>(window);
        if (newProfile == null || window.IsClosed)
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
            it.Bind(FormattedTextBlock.Arg1Property, new Binding()
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
            it.Bind(FormattedTextBlock.Arg1Property, new Binding()
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
        menuItem.Bind(IsEnabledProperty, new Binding() 
        { 
            Path = $"!{nameof(CurrentLogProfile)}.{nameof(LogProfile.IsBuiltIn)}",
            Source = this,
        });
        menuItem.Tag = EditCurrentLogProfileTag;
    });


    // Create menu item for exporting current log profile.
    MenuItem CreateExportCurrentLogProfileMenuItem() => new MenuItem().Also(menuItem =>
    {
        menuItem.Command = new CarinaStudio.Windows.Input.Command(this.ExportCurrentLogProfile);
        menuItem.Header = new FormattedTextBlock().Also(it =>
        {
            it.Bind(FormattedTextBlock.Arg1Property, new Binding()
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
        menuItem.Click += (_, e) =>
        {
            this.Close();
            this.LogProfileSelected?.Invoke(this, logProfile);
        };
        menuItem.DataContext = logProfile;
        menuItem.Icon = new Avalonia.Controls.Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Bind(Avalonia.Controls.Image.SourceProperty, new Binding()
            {
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
                it.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            });
            var currentLogProfileTextBlock = new Avalonia.Controls.TextBlock().Also(it =>
            {
                it.Opacity = this.FindResourceOrDefault<double>("Double/LogProfileSelectionContextMenu.CurrentLogProfile.Opacity");
                it.Bind(Avalonia.Controls.TextBlock.TextProperty, this.GetResourceObservable("String/LogProfileSelectionContextMenu.CurrentLogProfile"));
                it.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                Grid.SetColumn(it, 2);
            });
            grid.Children.Add(nameTextBlock);
            grid.Children.Add(new Separator().Also(it =>
            {
                it.Classes.Add("Dialog_Separator_Small");
                it.Bind(IsVisibleProperty, new Binding()
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
                if (currentLogProfile == null || dataContext != currentLogProfile)
                {
                    nameTextBlock.FontWeight = Avalonia.Media.FontWeight.Normal;
                    currentLogProfileTextBlock.IsVisible = false;
                }
                else
                {
                    nameTextBlock.FontWeight = Avalonia.Media.FontWeight.Bold;
                    currentLogProfileTextBlock.IsVisible = true;
                }
            });
        });
        menuItem.Bind(MenuItem.IsEnabledProperty, new MultiBinding().Also(it =>
        {
            it.Bindings.Add(new Binding()
            {
                Path = "IsProVersionActivated",
                Source = this,
            });
            it.Bindings.Add(new Binding()
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
            it.Bind(FormattedTextBlock.Arg1Property, new Binding()
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
        menuItem.Bind(IsEnabledProperty, new Binding() 
        { 
            Path = $"!{nameof(CurrentLogProfile)}.{nameof(LogProfile.IsBuiltIn)}",
            Source = this,
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
    void ExportCurrentLogProfile()
    {
        var logProfile = this.CurrentLogProfile;
        var window = this.FindLogicalAncestorOfType<CarinaStudio.Controls.Window>();
        if (logProfile == null || window == null)
            return;
        _ = logProfile.ExportAsync(window);
    } 


    /// <inheritdoc/>
    Type IStyleable.StyleKey => typeof(ContextMenu);


    /// <summary>
    /// Get items of menu.
    /// </summary>
    public new object? Items { get; }


    /// <summary>
    /// Raised when new log profile was just created.
    /// </summary>
    public event Action<LogProfileSelectionContextMenu, LogProfile>? LogProfileCreated;


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
        this.isAttachedToLogicalTree = true;
        (LogProfileManager.Default.Profiles as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnLogProfilesChanged);
        (LogProfileManager.Default.RecentlyUsedProfiles as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnRecentlyUsedLogProfilesChanged);
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
            }
            var recentlyUsedLogProfiles = LogProfileManager.Default.RecentlyUsedProfiles;
            foreach (var logProfile in LogProfileManager.Default.Profiles)
            {
                logProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
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
                it.ProductActivationChanged += this.OnProductActivationChanged;
            }
        });
    }


    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        this.isAttachedToLogicalTree = false;
        (LogProfileManager.Default.Profiles as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnLogProfilesChanged);
        (LogProfileManager.Default.RecentlyUsedProfiles as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnRecentlyUsedLogProfilesChanged);
        foreach (var logProfile in LogProfileManager.Default.Profiles)
            logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
        this.items.Clear();
        this.pinnedLogProfiles.Clear();
        this.recentlyUsedLogProfiles.Clear();
        App.Current.ProductManager.ProductActivationChanged -= this.OnProductActivationChanged;
        base.OnDetachedFromLogicalTree(e);
    }


    // Called when property of log profile changed.
    void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LogProfile logProfile)
            return;
        switch (e.PropertyName)
        {
            case nameof(LogProfile.IsPinned):
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
                        && this.recentlyUsedLogProfiles.Add(logProfile)
                        && this.recentlyUsedLogProfiles.Count == 1)
                    {
                        this.items.Add(this.recentlyUsedLogProfilesSeparator);
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
                if (this.TryFindMenuItem(logProfile, out var menuItem))
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
                    logProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
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
            case NotifyCollectionChangedAction.Remove:
                foreach (var logProfile in e.OldItems!.Cast<LogProfile>())
                {
                    logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
                    this.items.RemoveAll(it =>
                        it is MenuItem menuItem && menuItem.DataContext == logProfile);
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


    // Try find menu item for given log profile.
    bool TryFindMenuItem(LogProfile logProfile, [NotNullWhen(true)] out MenuItem? menuItem)
    {
        menuItem = this.items.FirstOrDefault(it => it is MenuItem menuItem && menuItem.DataContext == logProfile) as MenuItem;
        return menuItem != null;
    }


    // Update visibility of actions on current log profile.
    void UpdateActionOnCurrentLogProfileMenuItemsVisibility()
    {
        if (this.isAttachedToLogicalTree)
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