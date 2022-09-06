using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using CarinaStudio.Collections;
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
    /// Property of <see cref="ShowPinnedLogProfiles"/>.
    /// </summary>
    public static readonly StyledProperty<bool> ShowPinnedLogProfilesProperty = AvaloniaProperty.Register<LogProfileSelectionContextMenu, bool>(nameof(ShowPinnedLogProfiles), true);


    // Fields.
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
        this.Items = this.items.AsReadOnly();
        this.MenuClosed += (_, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (this.items.IsNotEmpty())
                    this.ScrollIntoView(0);
            }, Avalonia.Threading.DispatcherPriority.Normal);
        };
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
                return 0;
            if (((rhs as MenuItem)?.DataContext as LogProfile)?.IsPinned == true)
                return 1;
            return -1;
        }
        if (rhs is Separator)
        {
            if (((lhs as MenuItem)?.DataContext as LogProfile)?.IsPinned == true)
                return -1;
            return 1;
        }
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


    // Create menu item for log profile.
    MenuItem CreateMenuItem(LogProfile logProfile) => new MenuItem().Also(menuItem =>
    {
        menuItem.Click += (_, e) =>
        {
            this.Close();
            this.LogProfileSelected?.Invoke(this, logProfile);
        };
        menuItem.DataContext = logProfile;
        menuItem.Icon = new Image().Also(icon =>
        {
            icon.Classes.Add("MenuItem_Icon");
            icon.Bind(Image.SourceProperty, new Binding()
            {
                Converter = LogProfileIconConverter.Default,
                Path = nameof(LogProfile.Icon),
            });
        });
        menuItem.Bind(MenuItem.HeaderProperty, new Binding() 
        { 
            Path = nameof(LogProfile.Name) 
        });
    });


    /// <inheritdoc/>
    Type IStyleable.StyleKey => typeof(ContextMenu);


    /// <summary>
    /// Get items of menu.
    /// </summary>
    public new object? Items { get; }


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


    /// <summary>
    /// Get or set whether pinned log profiles should be shown or not.
    /// </summary>
    public bool ShowPinnedLogProfiles
    {
        get => this.GetValue<bool>(ShowPinnedLogProfilesProperty);
        set => this.SetValue<bool>(ShowPinnedLogProfilesProperty, value);
    }
}