using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Manager of <see cref="LogProfile"/>.
/// </summary>
class LogProfileManager : BaseProfileManager<IULogViewerApplication, LogProfile>
{
    // Static fields.
    static readonly IList<string> builtInProfileIDs = new List<string>()
    {
        "AndroidDeviceEventLog",
        "AndroidDeviceLog",
        "AndroidFileLog",
        "AndroidKernelLogFile",
        "AndroidTraceFile",
#if DEBUG
        "DummyLog",
#endif
        "GitLog",
        "GitLogSimple",
        "LinuxKernelLogFile",
        "LinuxSystemLogFile",
        "MacOSSystemLogFile",
        "RawFile",
        "RawHttp",
        "RawTcpServer",
        "TcpNLog",
    };
    static LogProfileManager? defaultInstance;


    // Fields.
    readonly SortedObservableList<LogProfile> pinnedProfiles;


    // Static initializer.
    static LogProfileManager()
    {
        if (Platform.IsWindows)
        {
            builtInProfileIDs.Add("WindowsApplicationEventLogs");
            builtInProfileIDs.Add("WindowsSecurityEventLogs");
            builtInProfileIDs.Add("WindowsSetupEventLogs");
            builtInProfileIDs.Add("WindowsSystemEventLogs");
        }
        else if (Platform.IsLinux)
        {
            builtInProfileIDs.Add("LinuxKernelLog");
            builtInProfileIDs.Add("LinuxSystemLog");
        }
        else if (Platform.IsMacOS)
        {
            builtInProfileIDs.Add("MacOSInstallationLog");
            builtInProfileIDs.Add("MacOSRealtimeLog");
        }
    }


    // Constructor.
    LogProfileManager(IULogViewerApplication app) : base(app)
    { 
        if (app.IsDebugMode)
        {
            builtInProfileIDs.Add("ULogViewerLog");
            builtInProfileIDs.Add("ULogViewerMemoryLog");
        }
        this.EmptyProfile = LogProfile.CreateEmptyBuiltInProfile(app);
        this.pinnedProfiles = new(this.CompareProfiles);
        this.PinnedProfiles = (IReadOnlyList<LogProfile>)this.pinnedProfiles.AsReadOnly();
        app.StringsUpdated += this.OnApplicationStringsUpdated;
    }


    /// <summary>
    /// Add log profile.
    /// </summary>
    /// <param name="profile">Profile.</param>
    public void AddProfile(LogProfile profile)
    {
        this.VerifyAccess();
        if (profile.Manager != null)
            throw new InvalidOperationException();
        if (this.GetProfileOrDefault(profile.Id) != null)
            profile.ChangeId();
        base.AddProfile(profile, true);
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static LogProfileManager Default { get => defaultInstance ?? throw new InvalidOperationException(); }


    /// <summary>
    /// Get empty profile.
    /// </summary>
    public LogProfile EmptyProfile { get; }


    /// <summary>
    /// Initialize <see cref="LogProfileManager"/> asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static async Task InitializeAsync(IULogViewerApplication app)
    {
        // check state
        if (defaultInstance != null)
            throw new InvalidOperationException();
        
        // create manager
        defaultInstance = new(app);
        defaultInstance.Logger.LogTrace("Start initialization");

        // load build-in profiles
        defaultInstance.Logger.LogDebug("Start loading built-in profiles");
        var profileCount = 0;
        foreach (var id in builtInProfileIDs)
        {
            defaultInstance.Logger.LogDebug($"Load '{id}'");
            defaultInstance.AddProfile(await LogProfile.LoadBuiltInAsync(app, id), false);
            ++profileCount;
        }
        defaultInstance.Logger.LogDebug($"Complete loading {profileCount} built-in profile(s)");

        // load profiles
        profileCount = 0;
        defaultInstance.Logger.LogDebug("Start loading profiles");
        var fileNames = await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(defaultInstance.ProfilesDirectory))
                    return new string[0];
                return Directory.GetFiles(defaultInstance.ProfilesDirectory, "*.json");
            }
            catch (Exception ex)
            {
                defaultInstance.Logger.LogError(ex, $"Unable to check profiles in directory '{defaultInstance.ProfilesDirectory}'");
                return new string[0];
            }
        });
        foreach (var fileName in fileNames)
        {
            try
            {
                var profile = await LogProfile.LoadAsync(app, fileName);
                if (Path.GetFileNameWithoutExtension(fileName) != profile.Id)
                {
                    defaultInstance.AddProfile(profile);
                    defaultInstance.Logger.LogWarning($"Delete legacy profile file '{fileName}'");
                    Global.RunWithoutErrorAsync(() => System.IO.File.Delete(fileName));
                }
                else
                    defaultInstance.AddProfile(profile, false);
                ++profileCount;
            }
            catch (Exception ex)
            {
                defaultInstance.Logger.LogError(ex, $"Unable to load profile from '{fileName}'");
            }
        }
        defaultInstance.Logger.LogDebug($"Complete loading {profileCount} profile(s)");

        // complete
        defaultInstance.Logger.LogTrace("Complete initialization");
    }


    // Called when application string resources updated.
    void OnApplicationStringsUpdated(object? sender, EventArgs e)
    {
        foreach (var profile in this.Profiles)
            profile.OnApplicationStringsUpdated();
    }


    /// <inheritdoc/>
    protected override void OnAttachToProfile(LogProfile profile)
    {
        base.OnAttachToProfile(profile);
        if (profile.IsPinned)
            this.pinnedProfiles.Add(profile);
    }


    /// <inheritdoc/>
    protected override void OnDetachFromProfile(LogProfile profile)
    {
         this.pinnedProfiles.Remove(profile);
        base.OnDetachFromProfile(profile);
    }


    /// <inheritdoc/>
    protected override void OnProfilePropertyChanged(LogProfile profile, PropertyChangedEventArgs e)
    {
        base.OnProfilePropertyChanged(profile, e);
        if (e.PropertyName == nameof(LogProfile.IsPinned))
        {
            if (profile.IsPinned)
                this.pinnedProfiles.Add(profile);
            else
                this.pinnedProfiles.Remove(profile);
        }
    }


    /// <summary>
    /// Get list of pinned <see cref="LogProfile"/>s.
    /// </summary>
    /// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
    public IReadOnlyList<LogProfile> PinnedProfiles { get; }


    /// <inheritdoc/>
    protected override string ProfilesDirectory { get => Path.Combine(this.Application.RootPrivateDirectoryPath, "Profiles"); }


    /// <summary>
    /// Remove log profile.
    /// </summary>
    /// <param name="profile">Profile to remove.</param>
    /// <returns>True if profile has neem removed successfully.</returns>
    public new bool RemoveProfile(LogProfile profile) =>
        base.RemoveProfile(profile);
}