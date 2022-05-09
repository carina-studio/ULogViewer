using CarinaStudio.AppSuite.Data;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
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
    /// Initialize asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static async Task InitializeAsync(IULogViewerApplication app)
    {
        // check state
        if (defaultInstance != null)
            throw new InvalidOperationException();
        
        // create instance
        defaultInstance = new(app);
        defaultInstance.Logger.LogTrace("Start initialization");

        // find filter files
        var filterFileNames = await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(defaultInstance.ProfilesDirectory))
                    return Directory.GetFiles(defaultInstance.ProfilesDirectory, "*.json");
                return new string[0];
            }
            catch(Exception ex)
            {
                defaultInstance.Logger.LogError(ex, $"Unable to find filter files in '{defaultInstance.ProfilesDirectory}'");
                return new string[0];
            }
        });
        defaultInstance.Logger.LogDebug($"Found {filterFileNames.Length} filter file(s)");

        // load filters
        foreach (var fileName in filterFileNames)
        {
            try
            {
                var filter = await PredefinedLogTextFilter.LoadAsync(app, fileName);
                if (Path.GetFileNameWithoutExtension(fileName) != filter.Id)
                {
                    defaultInstance.Logger.LogWarning($"Delete legacy filter file '{fileName}'");
                    defaultInstance.AddProfile(filter ,true);
                    Global.RunWithoutErrorAsync(() => File.Delete(fileName));
                }
                else
                    defaultInstance.AddProfile(filter ,false);
            }
            catch (Exception ex)
            {
                defaultInstance.Logger.LogError(ex, $"Unable to load filter from file '{fileName}'");
            }
        }
        defaultInstance.Logger.LogDebug($"{defaultInstance.Profiles.Count} filter(s) loaded");
        
        // complete
        defaultInstance.Logger.LogTrace("Complete initialization");
    }


    /// <inheritdoc/>
    protected override string ProfilesDirectory { get => Path.Combine(this.Application.RootPrivateDirectoryPath, "TextFilters"); }


    /// <summary>
    /// Remove text filter.
    /// </summary>
    /// <param name="filter">Filter.</param>
    /// <returns>True if filter has been removed successfully.</returns>
    public bool RemoveFilter(PredefinedLogTextFilter filter) =>
        base.RemoveProfile(filter);
}