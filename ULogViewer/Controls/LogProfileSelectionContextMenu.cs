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
    /// <summary>
    /// Property of <see cref="ShowPinnedLogProfiles"/>.
    /// </summary>
    public static readonly StyledProperty<bool> ShowPinnedLogProfilesProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>(nameof(ShowPinnedLogProfiles), true);


    // Static fields.
    static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>("IsProVersionActivated", false);


    // Fields.
    Separator? actionsOnCurrentLogProfileSeparator;
    MenuItem? editCurrentLogProfileMenuItem;
    MenuItem? exportCurrentLogProfileMenuItem;
    bool isAttachedToLogicalTree;
    readonly SortedObservableList<object> items;
    readonly HashSet<LogProfile> pinnedLogProfiles = new();
    readonly Separator pinnedLogProfilesSeparator = new();


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
        this.GetObservable(ShowPinnedLogProfilesProperty).Subscribe(show =>
        {
            if (!this.isAttachedToLogicalTree)
                return;
            var items = new List<object>(this.items);
            if (show)
            {
                if (this.pinnedLogProfiles.IsNotEmpty())
                    items.Add(this.pinnedLogProfilesSeparator);
            }
            else
                items.Remove(this.pinnedLogProfilesSeparator);
            this.items.Clear();
            this.items.AddAll(items);
        });
    }


    // Compare menu items.
    int CompareItems(object? lhs, object? rhs)
    {
        if (lhs is Separator)
        {
            if (rhs is Separator)
            {
                if (lhs == rhs)
                    return 0;
                if (lhs == this.actionsOnCurrentLogProfileSeparator)
                    return -1;
                return 1;
            }
            if (rhs == this.editCurrentLogProfileMenuItem
                || rhs == this.exportCurrentLogProfileMenuItem)
            {
                return 1;
            }
            if (lhs == this.actionsOnCurrentLogProfileSeparator)
                return -1;
            if (((rhs as MenuItem)?.DataContext as LogProfile)?.IsPinned == true)
                return 1;
            return -1;
        }
        if (rhs is Separator)
        {
            if (lhs == this.editCurrentLogProfileMenuItem
                || lhs == this.exportCurrentLogProfileMenuItem)
            {
                return -1;
            }
            if (rhs == this.actionsOnCurrentLogProfileSeparator)
                return 1;
            if (((lhs as MenuItem)?.DataContext as LogProfile)?.IsPinned == true)
                return -1;
            return 1;
        }
        if (lhs == this.editCurrentLogProfileMenuItem)
            return -1;
        if (rhs == this.editCurrentLogProfileMenuItem)
            return 1;
        if (lhs == this.exportCurrentLogProfileMenuItem)
            return -1;
        if (rhs == this.exportCurrentLogProfileMenuItem)
            return 1;
        var lhsLogProfile = (LogProfile)((MenuItem)lhs!).DataContext!;
        var rhsLogProfile = (LogProfile)((MenuItem)rhs!).DataContext!;
        if (this.GetValue<bool>(ShowPinnedLogProfilesProperty))
        {
            if (lhsLogProfile.IsPinned)
            {
                if (rhsLogProfile.IsPinned)
                    return CompareLogProfiles(lhsLogProfile, rhsLogProfile);
                return -1;
            }
            if (rhsLogProfile.IsPinned)
                return 1;
        }
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


    /// <summary>
    /// Get or set current log profile.
    /// </summary>
    public LogProfile? CurrentLogProfile
    {
        get => this.GetValue(CurrentLogProfileProperty);
        set => this.SetValue(CurrentLogProfileProperty, value);
    }


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
            icon.Source = this.FindResourceOrDefault<IImage>("Image/Icon.Edit");
        });
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


    // Edit current log profile.
    async void EditCurrentLogProfile()
    {
        // get state
        var logProfile = this.CurrentLogProfile;
        var window = this.FindLogicalAncestorOfType<CarinaStudio.Controls.Window>();
        var app = App.CurrentOrNull;
        if (logProfile == null || window == null || app == null)
            return;
        
        // copy or edit log profile
        if (logProfile.IsBuiltIn)
        {
            // show message
            await new MessageDialog()
            {
                Icon = MessageDialogIcon.Information,
                Message = new FormattedString().Also(it =>
                {
                    it.Arg1 = logProfile.Name;
                    it.Bind(FormattedString.FormatProperty, app.GetObservableString("LogProfileSelectionContextMenu.ConfirmEditingBuiltInLogProfile"));
                }),
                Title = app.GetObservableString("LogProfileSelectionDialog.EditLogProfile"),
            }.ShowDialog(window);
            if (window.IsClosed)
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
        else
            LogProfileEditorDialog.Show(window, logProfile);
    }


    /// <summary>
    /// Get or set whether item of editing current log profile is visible or not.
    /// </summary>
    public bool EnableActionsOnCurrentLogProfile
    {
        get => this.GetValue<bool>(EnableActionsOnCurrentLogProfileProperty);
        set => this.SetValue<bool>(EnableActionsOnCurrentLogProfileProperty, value);
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
        this.items.AddAll(new List<object>().Also(it =>
        {
            if (this.CurrentLogProfile != null && this.EnableActionsOnCurrentLogProfile)
            {
                this.editCurrentLogProfileMenuItem ??= this.CreateEditCurrentLogProfileMenuItem();
                this.exportCurrentLogProfileMenuItem ??= this.CreateExportCurrentLogProfileMenuItem();
                this.actionsOnCurrentLogProfileSeparator ??= new();
                it.Add(this.editCurrentLogProfileMenuItem);
                it.Add(this.exportCurrentLogProfileMenuItem);
                it.Add(this.actionsOnCurrentLogProfileSeparator);
            }
            foreach (var logProfile in LogProfileManager.Default.Profiles)
            {
                logProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
                if (logProfile.IsTemplate)
                    continue;
                if (logProfile.IsPinned)
                    this.pinnedLogProfiles.Add(logProfile);
                it.Add(this.CreateMenuItem(logProfile));
            }
            if (this.GetValue<bool>(ShowPinnedLogProfilesProperty) && this.pinnedLogProfiles.IsNotEmpty())
                it.Add(this.pinnedLogProfilesSeparator);
        }));
        App.Current.ProductManager.Let(it =>
        {
            if (it.IsMock)
                this.SetValue<bool>(IsProVersionActivatedProperty, false);
            else
            {
                this.SetValue<bool>(IsProVersionActivatedProperty, it.IsProductActivated(Products.Professional));
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
        foreach (var logProfile in LogProfileManager.Default.Profiles)
            logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
        this.items.Clear();
        this.pinnedLogProfiles.Clear();
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
                        && this.pinnedLogProfiles.Count == 1
                        && this.GetValue<bool>(ShowPinnedLogProfilesProperty))
                    {
                        this.items.Add(this.pinnedLogProfilesSeparator);
                    }
                }
                else if (this.pinnedLogProfiles.Remove(logProfile)
                    && this.pinnedLogProfiles.IsEmpty()
                    && this.GetValue<bool>(ShowPinnedLogProfilesProperty))
                {
                    this.items.Remove(this.pinnedLogProfilesSeparator);
                }
                break;
            case nameof(LogProfile.IsTemplate):
                if (logProfile.IsTemplate)
                {
                    this.items.RemoveAll(it =>
                        it is MenuItem menuItem && menuItem.DataContext == logProfile);
                    if (this.pinnedLogProfiles.Remove(logProfile)
                        && this.pinnedLogProfiles.IsEmpty()
                        && this.GetValue<bool>(ShowPinnedLogProfilesProperty))
                    {
                        this.items.Remove(this.pinnedLogProfilesSeparator);
                    }
                }
                else
                {
                    this.items.Add(this.CreateMenuItem(logProfile));
                    if (logProfile.IsPinned 
                        && this.pinnedLogProfiles.Add(logProfile)
                        && this.pinnedLogProfiles.Count == 1
                        && this.GetValue<bool>(ShowPinnedLogProfilesProperty))
                    {
                        this.items.Add(this.pinnedLogProfilesSeparator);
                    }
                }
                break;
            case nameof(LogProfile.Name):
                this.items.FirstOrDefault(it =>
                {
                    if (it is not MenuItem menuItem)
                        return false;
                    return menuItem.DataContext == logProfile;
                })?.Let(it => this.items.Sort(it));
                break;
        }
    }


    // Called when list of all log profiles changed.
    void OnLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var isPinnedLogProfileSeparatorShown = this.pinnedLogProfiles.IsNotEmpty()
            && this.GetValue<bool>(ShowPinnedLogProfilesProperty);
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
                    this.items.Add(this.CreateMenuItem(logProfile));
                }
                if (!isPinnedLogProfileSeparatorShown
                    && this.pinnedLogProfiles.IsNotEmpty()
                    && this.GetValue<bool>(ShowPinnedLogProfilesProperty))
                {
                    this.items.Add(this.pinnedLogProfilesSeparator);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var logProfile in e.OldItems!.Cast<LogProfile>())
                {
                    logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
                    this.items.RemoveAll(it =>
                        it is MenuItem menuItem && menuItem.DataContext == logProfile);
                    this.pinnedLogProfiles.Remove(logProfile);
                }
                if (isPinnedLogProfileSeparatorShown && this.pinnedLogProfiles.IsEmpty())
                    this.items.Remove(this.pinnedLogProfilesSeparator);
                break;
            default:
                throw new NotSupportedException($"Unsupported action of change of log profile list: {e.Action}");
        }
    }


    // Called when activation state of product changed.
    void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
    {
        if (productId == Products.Professional)
            this.SetValue<bool>(IsProVersionActivatedProperty, isActivated);
    }


    /// <summary>
    /// Get or set whether pinned log profiles should be shown or not.
    /// </summary>
    public bool ShowPinnedLogProfiles
    {
        get => this.GetValue<bool>(ShowPinnedLogProfilesProperty);
        set => this.SetValue<bool>(ShowPinnedLogProfilesProperty, value);
    }


    // Update visibility of actions on current log profile.
    void UpdateActionOnCurrentLogProfileMenuItemsVisibility()
    {
        if (this.isAttachedToLogicalTree)
        {
            if (this.CurrentLogProfile != null && this.EnableActionsOnCurrentLogProfile)
            {
                this.editCurrentLogProfileMenuItem ??= this.CreateEditCurrentLogProfileMenuItem();
                this.exportCurrentLogProfileMenuItem ??= this.CreateExportCurrentLogProfileMenuItem();
                this.actionsOnCurrentLogProfileSeparator ??= new();
                if (!this.items.Contains(this.actionsOnCurrentLogProfileSeparator))
                {
                    this.items.Add(this.editCurrentLogProfileMenuItem);
                    this.items.Add(this.exportCurrentLogProfileMenuItem);
                    this.items.Add(this.actionsOnCurrentLogProfileSeparator);
                }
            }
            else
            {
                if (this.editCurrentLogProfileMenuItem != null)
                    this.items.Remove(this.editCurrentLogProfileMenuItem);
                if (this.exportCurrentLogProfileMenuItem != null)
                    this.items.Remove(this.exportCurrentLogProfileMenuItem);
                if (this.actionsOnCurrentLogProfileSeparator != null)
                    this.items.Remove(this.actionsOnCurrentLogProfileSeparator);
            }
        }
    }
}