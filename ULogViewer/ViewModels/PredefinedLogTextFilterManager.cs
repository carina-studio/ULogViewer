using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Manager of <see cref="PredefinedLogTextFilter"/>.
/// </summary>
class PredefinedLogTextFilterManager : BaseProfileManager<IULogViewerApplication, PredefinedLogTextFilter>
{
    // Static fields.
    static PredefinedLogTextFilterManager? defaultInstance;


    // Constructor.
    PredefinedLogTextFilterManager(IULogViewerApplication app) : base(app)
    { }


    /// <summary>
    /// Add text filter.
    /// </summary>
    /// <param name="filter">Filter.</param>
    public void AddFilter(PredefinedLogTextFilter filter)
    {
        this.VerifyAccess();
        if (filter.Manager != null)
            throw new InvalidOperationException();
        if (this.GetProfileOrDefault(filter.Id) != null)
            filter.ChangeId();
        base.AddProfile(filter, true);
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static PredefinedLogTextFilterManager Default { get => defaultInstance ?? throw new InvalidOperationException(); }
    

    /// <summary>
    /// Get all filters.
    /// </summary>
    public IReadOnlyList<PredefinedLogTextFilter> Filters { get => base.Profiles; }


    /// <summary>
    /// Get filter with given ID.
    /// </summary>
    /// <param name="id">ID of filter.</param>
    /// <returns>Filter with given ID or Null if filter cannot be found.</returns>
    public PredefinedLogTextFilter? GetFilterOrDefault(string id) =>
        base.GetProfileOrDefault(id);


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


    /// <inheritdoc/>
    protected override Task<PredefinedLogTextFilter> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        PredefinedLogTextFilter.LoadAsync(this.Application, fileName);


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "TextFilters");


    /// <summary>
    /// Remove text filter.
    /// </summary>
    /// <param name="filter">Filter.</param>
    /// <returns>True if filter has been removed successfully.</returns>
    public bool RemoveFilter(PredefinedLogTextFilter filter) =>
        base.RemoveProfile(filter);
}