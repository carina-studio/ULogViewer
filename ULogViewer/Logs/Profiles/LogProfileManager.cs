using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
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
    /// Get log profile with given ID.
    /// </summary>
    /// <param name="id">ID of log profile.</param>
    /// <returns>Log profile with given ID or Null if profile cannot be found.</returns>
    public new LogProfile? GetProfileOrDefault(string id) =>
        base.GetProfileOrDefault(id);


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
        
        // initialize
        defaultInstance = new(app);
        await defaultInstance.WaitForInitialization();
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
    protected override async Task<ICollection<LogProfile>> OnLoadBuiltInProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = new List<LogProfile>();
        foreach (var id in builtInProfileIDs)
        {
            this.Logger.LogDebug($"Load '{id}'");
            profiles.Add(await LogProfile.LoadBuiltInAsync(this.Application, id));
        }
        return profiles;
    }


    /// <inheritdoc/>
    protected override Task<LogProfile> OnLoadProfileAsync(string fileName, CancellationToken cancellationToken = default) =>
        LogProfile.LoadAsync(this.Application, fileName);


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


    /// <summary>
    /// Get all log profiles.
    /// </summary>
    public new IReadOnlyList<LogProfile> Profiles { get => base.Profiles; }


    /// <inheritdoc/>
    protected override string ProfilesDirectory => Path.Combine(this.Application.RootPrivateDirectoryPath, "Profiles");


    /// <summary>
    /// Remove log profile.
    /// </summary>
    /// <param name="profile">Profile to remove.</param>
    /// <returns>True if profile has neem removed successfully.</returns>
    public new bool RemoveProfile(LogProfile profile) =>
        base.RemoveProfile(profile);
}