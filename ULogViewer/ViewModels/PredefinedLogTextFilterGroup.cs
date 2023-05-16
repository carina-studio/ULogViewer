using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// View-model of group of <see cref="PredefinedLogTextFilter"/>.
/// </summary>
class PredefinedLogTextFilterGroup : BaseDisposableApplicationObject<IULogViewerApplication>
{
    // Fields.
    readonly HashSet<PredefinedLogTextFilter> attachedFilters = new();
    readonly SortedObservableList<PredefinedLogTextFilter> filters = new((lhs, rhs) =>
    {
        var result = string.CompareOrdinal(lhs.Name, rhs.Name);
        return result != 0 ? result : string.CompareOrdinal(lhs.Id, rhs.Id);
    });


    /// <summary>
    /// Initialize new <see cref="PredefinedLogTextFilterGroup"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="name">Name of group.</param>
    public PredefinedLogTextFilterGroup(IULogViewerApplication app, string? name) : base(app)
    {
        this.Name = name;
        this.Filters = ListExtensions.AsReadOnly(this.filters.Also(it =>
        {
            it.CollectionChanged += (_, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (var filter in e.NewItems!.Cast<PredefinedLogTextFilter>())
                            this.AttachToFilter(filter);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        foreach (var filter in e.OldItems!.Cast<PredefinedLogTextFilter>())
                            this.DetachFromFilter(filter);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        break;
                    default:
                        throw new NotSupportedException();
                }
            };
        }));
    }


    /// <summary>
    /// Add filter to the group.
    /// </summary>
    /// <param name="filter">Filter.</param>
    protected void AddFilter(PredefinedLogTextFilter filter) =>
        this.filters.Add(filter);
    
    
    // Attach to filter.
    void AttachToFilter(PredefinedLogTextFilter filter)
    {
        if (!this.attachedFilters.Add(filter))
            return;
        filter.PropertyChanged += this.OnFilterPropertyChanged;
    }
    
    
    // Detach from filter.
    void DetachFromFilter(PredefinedLogTextFilter filter)
    {
        if (!this.attachedFilters.Remove(filter))
            return;
        filter.PropertyChanged -= this.OnFilterPropertyChanged;
    }
    
    
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;
        this.VerifyAccess();
        foreach (var filter in this.filters)
            this.DetachFromFilter(filter);
        this.filters.Clear();
    }
    
    
    /// <summary>
    /// Get list of filters in the group.
    /// </summary>
    public IList<PredefinedLogTextFilter> Filters { get; }


    // Called when property of filter changed.
    void OnFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PredefinedLogTextFilter filter && e.PropertyName == nameof(PredefinedLogTextFilter.Name))
            this.filters.Sort(filter);
    }
    
    
    /// <summary>
    /// Remove filter from the group.
    /// </summary>
    /// <param name="filter">Filter.</param>
    /// <returns>True if filter has been removed successfully.</returns>
    protected bool RemoveFilter(PredefinedLogTextFilter filter) =>
        this.filters.Remove(filter);
    
    
    /// <summary>
    /// Get name of group.
    /// </summary>
    public string? Name { get; }
}