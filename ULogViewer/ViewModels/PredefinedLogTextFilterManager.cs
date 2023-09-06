using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Manager of <see cref="PredefinedLogTextFilter"/>.
/// </summary>
class PredefinedLogTextFilterManager : BaseProfileManager<IULogViewerApplication, PredefinedLogTextFilter>
{
    // Actual implementation of filter group.
    class PredefinedLogTextFilterGroupImpl : PredefinedLogTextFilterGroup
    {
        // Constructor.
        public PredefinedLogTextFilterGroupImpl(PredefinedLogTextFilterManager manager, string? name) : base(manager.Application, name)
        { }
        
        // Add filter.
        public new void AddFilter(PredefinedLogTextFilter filter) =>
            base.AddFilter(filter);
        
        // Remove filter.
        public new bool RemoveFilter(PredefinedLogTextFilter filter) =>
            base.RemoveFilter(filter);
    }
    
    
    // Static fields.
    static PredefinedLogTextFilterManager? defaultInstance;
    
    
    // Fields.
    readonly HashSet<PredefinedLogTextFilter> changingGroupFilters = new();
    readonly SortedObservableList<PredefinedLogTextFilterGroup> groups = new((lhs, rhs) => string.CompareOrdinal(lhs.Name, rhs.Name));


    // Constructor.
    PredefinedLogTextFilterManager(IULogViewerApplication app) : base(app)
    {
        this.DefaultGroup = new PredefinedLogTextFilterGroupImpl(this, null);
        this.Groups = ListExtensions.AsReadOnly(this.groups);
    }


    /// <summary>
    /// Add text filter.
    /// </summary>
    /// <param name="filter">Filter.</param>
    public void AddFilter(PredefinedLogTextFilter filter)
    {
        // check state
        this.VerifyAccess();
        if (filter.Manager != null)
            throw new InvalidOperationException();
        
        // change ID if needed
        if (this.GetProfileOrDefault(filter.Id) != null)
            filter.ChangeId();
        
        // add profile
        this.AddProfile(filter, true);
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static PredefinedLogTextFilterManager Default => defaultInstance ?? throw new InvalidOperationException();
    
    
    /// <summary>
    /// Get default group of filters.
    /// </summary>
    public PredefinedLogTextFilterGroup DefaultGroup { get; }


    /// <summary>
    /// Get all filters.
    /// </summary>
    public IReadOnlyList<PredefinedLogTextFilter> Filters => this.Profiles;


    /// <summary>
    /// Get filter with given ID.
    /// </summary>
    /// <param name="id">ID of filter.</param>
    /// <returns>Filter with given ID or Null if filter cannot be found.</returns>
    public PredefinedLogTextFilter? GetFilterOrDefault(string id) =>
        this.GetProfileOrDefault(id);


    // Get current group or create new one.
    PredefinedLogTextFilterGroupImpl GetOrCreateGroup(string? groupName)
    {
        if (groupName is null)
            return (PredefinedLogTextFilterGroupImpl)this.DefaultGroup;
        var groupIndex = this.groups.BinarySearch(groupName, g => g.Name, string.CompareOrdinal);
        if (groupIndex >= 0)
            return (PredefinedLogTextFilterGroupImpl)this.groups[groupIndex];
        var group = new PredefinedLogTextFilterGroupImpl(this, groupName);
        this.groups.Add(group);
        return group;
    }


    /// <summary>
    /// Raised after changing group of filter.
    /// </summary>
    public event Action<PredefinedLogTextFilterManager, PredefinedLogTextFilter>? FilterGroupChanged;
    
    
    /// <summary>
    /// Raised before changing group of filter.
    /// </summary>
    public event Action<PredefinedLogTextFilterManager, PredefinedLogTextFilter>? FilterGroupChanging;


    /// <summary>
    /// Get all groups except for default group.
    /// </summary>
    public IList<PredefinedLogTextFilterGroup> Groups { get; }


    /// <summary>
    /// Initialize asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static async Task InitializeAsync(IULogViewerApplication app)
    {
        // check state
        if (defaultInstance != null)
            throw new InvalidOperationException();
        
        // initialize
        defaultInstance = new(app);
        await defaultInstance.WaitForInitialization();
    }


    /// <summary>
    /// Check whether group of given filter is being changed or not.
    /// </summary>
    /// <param name="filter">Filter.</param>
    /// <returns>True if group of given filter is being changed.</returns>
    public bool IsFilterGroupChanging(PredefinedLogTextFilter filter) =>
        this.changingGroupFilters.Contains(filter);


    /// <inheritdoc/>
    protected override void OnAttachToProfile(PredefinedLogTextFilter filter)
    {
        base.OnAttachToProfile(filter);
        this.GetOrCreateGroup(filter.GroupName).AddFilter(filter);
    }


    /// <inheritdoc/>
    protected override Task<PredefinedLogTextFilter> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        PredefinedLogTextFilter.LoadAsync(this.Application, fileName);


    /// <inheritdoc/>
    protected override void OnProfilePropertyChanged(PredefinedLogTextFilter filter, PropertyChangedEventArgs e)
    {
        base.OnProfilePropertyChanged(filter, e);
        if (e.PropertyName == nameof(PredefinedLogTextFilter.GroupName))
        {
            // check state
            if (!this.changingGroupFilters.Add(filter))
                throw new InvalidOperationException("Nested filter group change.");
                                
            // change group
            try
            {
                // remove from old group
                this.FilterGroupChanging?.Invoke(this, filter);
                if (!((PredefinedLogTextFilterGroupImpl)this.DefaultGroup).RemoveFilter(filter))
                {
                    // ReSharper disable PossibleInvalidCastExceptionInForeachLoop
                    foreach (PredefinedLogTextFilterGroupImpl group in this.groups)
                    {
                        if (group.RemoveFilter(filter))
                        {
                            if (group.Filters.IsEmpty())
                            {
                                this.groups.Remove(group);
                                group.Dispose();
                            }
                            break;
                        }
                    }
                    // ReSharper restore PossibleInvalidCastExceptionInForeachLoop
                }

                // add to new group
                this.GetOrCreateGroup(filter.GroupName).AddFilter(filter);
                this.FilterGroupChanged?.Invoke(this, filter);
            }
            finally
            {
                this.changingGroupFilters.Remove(filter);
            }
        }
    }


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "TextFilters");


    /// <summary>
    /// Remove text filter.
    /// </summary>
    /// <param name="filter">Filter.</param>
    /// <returns>True if filter has been removed successfully.</returns>
    public bool RemoveFilter(PredefinedLogTextFilter filter)
    {
        var group = this.GetOrCreateGroup(filter.GroupName);
        if (this.changingGroupFilters.Remove(filter))
            this.FilterGroupChanged?.Invoke(this, filter);
        if (group.RemoveFilter(filter) && group != this.DefaultGroup && group.Filters.IsEmpty())
        {
            this.groups.Remove(group);
            group.Dispose();
        }
        return this.RemoveProfile(filter, true);
    }


    /// <summary>
    /// Rename name of group.
    /// </summary>
    /// <param name="groupName">Name of group to rename.</param>
    /// <param name="newGroupName">New name of group.</param>
    public void RenameGroup(string groupName, string newGroupName)
    {
        // check state
        this.VerifyAccess();
        groupName = groupName.Trim();
        newGroupName = newGroupName.Trim();
        if (string.IsNullOrEmpty(newGroupName))
        {
            this.Logger.LogError("Cannot rename group to empty name");
            return;
        }
        if (groupName == newGroupName)
            return;
        
        // find group
        var group = this.groups.FirstOrDefault(it => it.Name == groupName);
        if (group is null)
        {
            this.Logger.LogWarning("Cannot find group '{name}'", groupName);
            return;
        }
        
        // move to new group
        this.Logger.LogDebug("Rename group '{name}' to '{newName}' with {count} filter(s)", groupName, newGroupName, group.Filters.Count);
        foreach (var filter in group.Filters.ToArray())
            filter.GroupName = newGroupName;
    }
}