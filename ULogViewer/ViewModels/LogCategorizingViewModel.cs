using System;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using CarinaStudio.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of log categorizing.
/// </summary>
class LogCategorizingViewModel : SessionComponent
{
    /// <summary>
    /// Property of <see cref="IsTimestampCategoriesPanelVisible"/>.
    /// </summary>
    public static readonly ObservableProperty<bool> IsTimestampCategoriesPanelVisibleProperty = ObservableProperty.Register<LogCategorizingViewModel, bool>(nameof(IsTimestampCategoriesPanelVisible), false);
    /// <summary>
    /// Property of <see cref="TimestampCategories"/>.
    /// </summary>
    public static readonly ObservableProperty<IReadOnlyList<TimestampDisplayableLogCategory>> TimestampCategoriesProperty = ObservableProperty.Register<LogCategorizingViewModel, IReadOnlyList<TimestampDisplayableLogCategory>>(nameof(TimestampCategories), Array.Empty<TimestampDisplayableLogCategory>());
    /// <summary>
    /// Property of <see cref="TimestampCategoriesPanelSize"/>.
    /// </summary>
    public static readonly ObservableProperty<double> TimestampCategoriesPanelSizeProperty = ObservableProperty.Register<LogCategorizingViewModel, double>(nameof(TimestampCategoriesPanelSize), (Session.MinSidePanelSize + Session.MaxSidePanelSize) / 2, 
        coerce: (_, it) =>
        {
            if (it >= Session.MaxSidePanelSize)
                return Session.MaxSidePanelSize;
            if (it < Session.MinSidePanelSize)
                return Session.MinSidePanelSize;
            return it;
        }, 
        validate: it => double.IsFinite(it));
    

    // Static fields.
    [Obsolete]
	static readonly SettingKey<double> latestSidePanelSizeKey = new("Session.LatestSidePanelSize", Session.MarkedLogsPanelSizeProperty.DefaultValue);
    static readonly SettingKey<double> latestTimestampCategoriesPanelSizeKey = new("Session.LatestTimestampCategoriesPanelSize", TimestampCategoriesPanelSizeProperty.DefaultValue);
        
    
    // Fields.
    readonly TimestampDisplayableLogCategorizer allLogsTimestampCategorizer;
    readonly IDisposable displayLogPropertiesObserverToken;
    TimestampDisplayableLogCategorizer? filteredLogsTimestampCategorizer;
    IDisposable isFilteringNeededObserverToken = EmptyDisposable.Default;
    bool isRestoringState;
    readonly IDisposable isShowingAllLogsObserverToken;
    readonly IDisposable isShowingMarkedLogsObserverToken;
    readonly TimestampDisplayableLogCategorizer markedLogsTimestampCategorizer;
    readonly ScheduledAction reportTimestampCategoriesAction;


    /// <summary>
    /// Initialize new <see cref="LogCategorizingViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogCategorizingViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // start initialization
        var isInit = true;

        // create categorizers
        this.allLogsTimestampCategorizer = new TimestampDisplayableLogCategorizer(this.Application, this.AllLogs, this.CompareLogs);
        this.markedLogsTimestampCategorizer = new TimestampDisplayableLogCategorizer(this.Application, session.MarkedLogs, this.CompareLogs);
        this.SetValue(TimestampCategoriesProperty, this.allLogsTimestampCategorizer.Categories);

        // setup scheduled actions
        this.reportTimestampCategoriesAction = new(() =>
        {
            if (this.IsDisposed)
                return;
            if (!this.Session.IsShowingAllLogsTemporarily)
            {
                if (this.Session.IsShowingMarkedLogsTemporarily)
                {
                    this.SetValue(TimestampCategoriesProperty, this.markedLogsTimestampCategorizer.Categories);
                    return;
                }
                else if (this.Session.LogFiltering.IsFilteringNeeded)
                {
                    this.SetValue(TimestampCategoriesProperty, this.filteredLogsTimestampCategorizer!.Categories);
                    return;
                }
            }
            this.SetValue(TimestampCategoriesProperty, this.allLogsTimestampCategorizer.Categories);
        });

        // attach to session
        this.displayLogPropertiesObserverToken = session.GetValueAsObservable(Session.DisplayLogPropertiesProperty).Subscribe(_ =>
        {
            if (!isInit)
                this.UpdateTimestampLogPropertyName();
        });
        this.isShowingAllLogsObserverToken = session.GetValueAsObservable(Session.IsShowingAllLogsTemporarilyProperty).Subscribe(_ =>
            this.reportTimestampCategoriesAction.Schedule());
        this.isShowingMarkedLogsObserverToken = session.GetValueAsObservable(Session.IsShowingMarkedLogsTemporarilyProperty).Subscribe(_ =>
            this.reportTimestampCategoriesAction.Schedule());
        
        // attach to self properties
        this.GetValueAsObservable(TimestampCategoriesPanelSizeProperty).Subscribe(size =>
        {
            if (!isInit && !this.isRestoringState)
                this.PersistentState.SetValue<double>(latestTimestampCategoriesPanelSizeKey, size);
        });
        
        // restore state
#pragma warning disable CS0612
        if (this.PersistentState.GetRawValue(latestSidePanelSizeKey) is double sidePanelSize)
            this.SetValue(TimestampCategoriesPanelSizeProperty, sidePanelSize);
        else
            this.SetValue(TimestampCategoriesPanelSizeProperty, this.PersistentState.GetValueOrDefault(latestTimestampCategoriesPanelSizeKey));
#pragma warning restore CS0612

        // complete
        isInit = false;
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // detach from components
        this.isFilteringNeededObserverToken.Dispose();

        // detach from session
        this.displayLogPropertiesObserverToken.Dispose();
        this.isShowingAllLogsObserverToken.Dispose();
        this.isShowingMarkedLogsObserverToken.Dispose();

        // release log categorizers
        this.allLogsTimestampCategorizer.Dispose();
        this.filteredLogsTimestampCategorizer?.Dispose();
        this.markedLogsTimestampCategorizer.Dispose();

        // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Get or set whether panel of timestamp categories is visible or not.
    /// </summary>
    public bool IsTimestampCategoriesPanelVisible 
    {
        get => this.GetValue(IsTimestampCategoriesPanelVisibleProperty);
        set => this.SetValue(IsTimestampCategoriesPanelVisibleProperty, value);
    }


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize
        + this.allLogsTimestampCategorizer.MemorySize
        + (this.filteredLogsTimestampCategorizer?.MemorySize ?? 0L)
        + this.markedLogsTimestampCategorizer.MemorySize;


    /// <inheritdoc/>
    protected override void OnAllComponentsCreated()
    {
        base.OnAllComponentsCreated();
        this.filteredLogsTimestampCategorizer = new TimestampDisplayableLogCategorizer(this.Application, this.Session.LogFiltering.FilteredLogs, this.CompareLogs);
        this.isFilteringNeededObserverToken = this.Session.LogFiltering.GetValueAsObservable(LogFilteringViewModel.IsFilteringNeededProperty).Subscribe(_ =>
            this.reportTimestampCategoriesAction.Schedule());
    }


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        // call base
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);

        // setup log categorizers
        (newLogProfile?.TimestampCategoryGranularity)?.Let(it =>
        {
            this.allLogsTimestampCategorizer.Granularity = it;
            this.filteredLogsTimestampCategorizer?.Let(c => c.Granularity = it);
            this.markedLogsTimestampCategorizer.Granularity = it;
        });
        this.UpdateTimestampLogPropertyName();
    }


    /// <inheritdoc/>
    protected override void OnLogProfilePropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnLogProfilePropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(LogProfile.SortKey):
                this.UpdateTimestampLogPropertyName();
                break;
            case nameof(LogProfile.TimestampCategoryGranularity):
                (this.LogProfile?.TimestampCategoryGranularity)?.Let(it =>
                {
                    this.allLogsTimestampCategorizer.Granularity = it;
                    this.filteredLogsTimestampCategorizer?.Let(c => c.Granularity = it);
                    this.markedLogsTimestampCategorizer.Granularity = it;
                });
                break;
        }
    }


    /// <inheritdoc/>
    protected override void OnRestoreState(JsonElement element)
    {
        this.isRestoringState = true;
        try
        {
            // call base
            base.OnRestoreState(element);

            // restore panel state
            if (element.TryGetProperty(nameof(IsTimestampCategoriesPanelVisible), out var jsonValue) // For upgrade case
                || element.TryGetProperty($"LogCategorizing.{nameof(IsTimestampCategoriesPanelVisible)}", out jsonValue))
            {
                this.SetValue(IsTimestampCategoriesPanelVisibleProperty, jsonValue.ValueKind != JsonValueKind.False);
            }
            if ((element.TryGetProperty(nameof(TimestampCategoriesPanelSize), out jsonValue) // For upgrade case
                || element.TryGetProperty($"LogCategorizing.{nameof(TimestampCategoriesPanelSize)}", out jsonValue))
                    && jsonValue.TryGetDouble(out var doubleValue)
                    && TimestampCategoriesPanelSizeProperty.ValidationFunction(doubleValue) == true)
            {
                this.SetValue(TimestampCategoriesPanelSizeProperty, doubleValue);
            }
        }
        finally
        {
            this.isRestoringState = false;
        }
    }


    /// <inheritdoc/>
    protected override void OnSaveState(Utf8JsonWriter writer)
    {
        // call base
        base.OnSaveState(writer);

        // save side panel state
        writer.WriteBoolean($"LogCategorizing.{nameof(IsTimestampCategoriesPanelVisible)}", this.IsTimestampCategoriesPanelVisible);
        writer.WriteNumber($"LogCategorizing.{nameof(TimestampCategoriesPanelSize)}", this.TimestampCategoriesPanelSize);
    }


    /// <summary>
    /// Get list of log categories by timestamp.
    /// </summary>
    public IReadOnlyList<TimestampDisplayableLogCategory> TimestampCategories { get => this.GetValue(TimestampCategoriesProperty); }


    /// <summary>
    /// Get or set size of panel of timestamp categories.
    /// </summary>
    public double TimestampCategoriesPanelSize
    {
        get => this.GetValue(TimestampCategoriesPanelSizeProperty);
        set => this.SetValue(TimestampCategoriesPanelSizeProperty, value);
    }


    // Update property of log for timestamp categorizing.
    void UpdateTimestampLogPropertyName()
    {
        var profile = this.LogProfile;
        if (profile == null)
            return;
        (profile.SortKey switch
        {
            LogSortKey.BeginningTimestamp => nameof(DisplayableLog.BinaryBeginningTimestamp),
            LogSortKey.EndingTimestamp => nameof(DisplayableLog.BinaryEndingTimestamp),
            LogSortKey.ReadTime => nameof(DisplayableLog.BinaryReadTime),
            LogSortKey.Timestamp => nameof(DisplayableLog.BinaryTimestamp),
            _ => Global.Run(() =>
            {
                foreach (var property in this.Session.DisplayLogProperties)
                {
                    switch (property.Name)
                    {
                        case nameof(DisplayableLog.BeginningTimestampString):
                            return nameof(DisplayableLog.BinaryBeginningTimestamp);
                        case nameof(DisplayableLog.EndingTimestampString):
                            return nameof(DisplayableLog.BinaryEndingTimestamp);
                        case nameof(DisplayableLog.TimestampString):
                            return nameof(DisplayableLog.BinaryTimestamp);
                        case nameof(DisplayableLog.ReadTimeString):
                            return nameof(DisplayableLog.BinaryReadTime);
                    }
                }
                return null;
            }),
        }).Let(propertyName =>
        {
            this.allLogsTimestampCategorizer.TimestampLogPropertyName = propertyName;
            this.filteredLogsTimestampCategorizer?.Let(c => c.TimestampLogPropertyName = propertyName);
            this.markedLogsTimestampCategorizer.TimestampLogPropertyName = propertyName;
        });
    }
}