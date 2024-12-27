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
    public static readonly ObservableProperty<double> TimestampCategoriesPanelSizeProperty = ObservableProperty.Register<LogCategorizingViewModel, double>(nameof(TimestampCategoriesPanelSize), Session.DefaultSidePanelSize, 
        coerce: (_, it) =>
        {
            if (it >= Session.MaxSidePanelSize)
                return Session.MaxSidePanelSize;
            if (it < Session.MinSidePanelSize)
                return Session.MinSidePanelSize;
            return it;
        }, 
        validate: double.IsFinite);
    

    // Static fields.
    [Obsolete]
	static readonly SettingKey<double> latestSidePanelSizeKey = new("Session.LatestSidePanelSize", Session.MarkedLogsPanelSizeProperty.DefaultValue);
    static readonly SettingKey<double> latestTimestampCategoriesPanelSizeKey = new("Session.LatestTimestampCategoriesPanelSize", TimestampCategoriesPanelSizeProperty.DefaultValue);
        
    
    // Fields.
    readonly TimestampDisplayableLogCategorizer allLogsTimestampCategorizer;
    TimestampDisplayableLogCategorizer? filteredLogsTimestampCategorizer;
    bool isRestoringState;
    readonly TimestampDisplayableLogCategorizer markedLogsTimestampCategorizer;
    readonly ScheduledAction reportTimestampCategoriesAction;


    /// <summary>
    /// Initialize new <see cref="LogCategorizingViewModel"/> instance.
    /// </summary>
    /// <param name="session">Session.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    public LogCategorizingViewModel(Session session, ISessionInternalAccessor internalAccessor) : base(session, internalAccessor)
    {
        // create categorizers
        var logComparer = new DisplayableLogComparer(this.CompareLogs, default);
        this.allLogsTimestampCategorizer = new TimestampDisplayableLogCategorizer(this.Application, this.AllLogs, logComparer);
        this.markedLogsTimestampCategorizer = new TimestampDisplayableLogCategorizer(this.Application, session.MarkedLogs, logComparer);
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
        var isAttachingToSession = true;
        this.AddResources(
            session.GetValueAsObservable(Session.DisplayLogPropertiesProperty).Subscribe(_ =>
            {
                if (!isAttachingToSession)
                    this.UpdateTimestampLogPropertyName();
            }),
            session.GetValueAsObservable(Session.IsShowingAllLogsTemporarilyProperty).Subscribe(_ =>
                this.reportTimestampCategoriesAction.Schedule()),
            session.GetValueAsObservable(Session.IsShowingMarkedLogsTemporarilyProperty).Subscribe(_ =>
                this.reportTimestampCategoriesAction.Schedule())
        );
        isAttachingToSession = false;
        
        // restore state
#pragma warning disable CS0612
        if (this.PersistentState.GetRawValue(latestSidePanelSizeKey) is double sidePanelSize)
            this.SetValue(TimestampCategoriesPanelSizeProperty, sidePanelSize);
        else
            this.SetValue(TimestampCategoriesPanelSizeProperty, this.PersistentState.GetValueOrDefault(latestTimestampCategoriesPanelSizeKey));
#pragma warning restore CS0612
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
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
        this.filteredLogsTimestampCategorizer = new TimestampDisplayableLogCategorizer(this.Application, this.Session.LogFiltering.FilteredLogs, new DisplayableLogComparer(this.CompareLogs, default));
        this.AddResource(this.Session.LogFiltering.GetValueAsObservable(LogFilteringViewModel.IsFilteringNeededProperty).Subscribe(_ =>
            this.reportTimestampCategoriesAction.Schedule())
        );
    }


    /// <inheritdoc/>
    protected override void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        // call base
        base.OnLogProfileChanged(prevLogProfile, newLogProfile);

        // setup log categorizers
        newLogProfile?.Let(profile =>
        {
            profile.SortDirection.Let(it =>
            {
                var comparer = new DisplayableLogComparer(this.CompareLogs, it);
                this.allLogsTimestampCategorizer.SourceLogComparer = comparer;
                if (this.filteredLogsTimestampCategorizer is not null)
                    this.filteredLogsTimestampCategorizer.SourceLogComparer = comparer;
                this.markedLogsTimestampCategorizer.SourceLogComparer = comparer;
            });
            profile.TimestampCategoryGranularity.Let(it =>
            {
                this.allLogsTimestampCategorizer.Granularity = it;
                this.filteredLogsTimestampCategorizer?.Let(c => c.Granularity = it);
                this.markedLogsTimestampCategorizer.Granularity = it;
            });
        });
        this.UpdateTimestampLogPropertyName();
    }


    /// <inheritdoc/>
    protected override void OnLogProfilePropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnLogProfilePropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(LogProfile.SortDirection):
                (this.LogProfile?.SortDirection)?.Let(it =>
                {
                    var comparer = new DisplayableLogComparer(this.CompareLogs, it);
                    this.allLogsTimestampCategorizer.SourceLogComparer = comparer;
                    if (this.filteredLogsTimestampCategorizer is not null)
                        this.filteredLogsTimestampCategorizer.SourceLogComparer = comparer;
                    this.markedLogsTimestampCategorizer.SourceLogComparer = comparer;
                });
                break;
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
    protected override void OnPropertyChanged(ObservableProperty property, object? oldValue, object? newValue)
    {
        base.OnPropertyChanged(property, oldValue, newValue);
        if (property == TimestampCategoriesPanelSizeProperty)
        {
            if (!this.isRestoringState)
                this.PersistentState.SetValue<double>(latestTimestampCategoriesPanelSizeKey, (double)newValue!);
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
                    && TimestampCategoriesPanelSizeProperty.ValidationFunction(doubleValue))
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
    public IReadOnlyList<TimestampDisplayableLogCategory> TimestampCategories => this.GetValue(TimestampCategoriesProperty);


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
        if (profile is null)
            return;
        (profile.SortKey switch
        {
            LogSortKey.BeginningTimestamp => nameof(DisplayableLog.BeginningTimestamp),
            LogSortKey.EndingTimestamp => nameof(DisplayableLog.EndingTimestamp),
            LogSortKey.ReadTime => nameof(DisplayableLog.ReadTime),
            LogSortKey.Timestamp => nameof(DisplayableLog.Timestamp),
            _ => Global.Run(() =>
            {
                foreach (var property in this.Session.DisplayLogProperties)
                {
                    switch (property.Name)
                    {
                        case nameof(DisplayableLog.BeginningTimestampString):
                            return nameof(DisplayableLog.BeginningTimestamp);
                        case nameof(DisplayableLog.EndingTimestampString):
                            return nameof(DisplayableLog.EndingTimestamp);
                        case nameof(DisplayableLog.TimestampString):
                            return nameof(DisplayableLog.Timestamp);
                        case nameof(DisplayableLog.ReadTimeString):
                            return nameof(DisplayableLog.ReadTime);
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