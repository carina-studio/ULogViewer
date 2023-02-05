using System.Text;
using System.Linq;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Manager of <see cref="LogProfile"/>.
/// </summary>
class LogProfileManager : BaseProfileManager<IULogViewerApplication, LogProfile>
{
    // Constants.
    const int RecentlyUsedProfileCount = 8;


    // Static fields.
    static readonly IList<string> builtInProfileIDs = new List<string>()
    {
        "AndroidDeviceEventLog",
        "AndroidDeviceLog",
        "AndroidFileLog",
        "AndroidKernelLogFile",
        "AndroidTraceFile",
        "ApacheAccessLogFile",
        "ApacheErrorLogFile",
        "AzureWebappLogFile",
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
        "ULogViewerLog",
        "ULogViewerMemoryLog",
    };
    static LogProfileManager? defaultInstance;
    static readonly SettingKey<string> recentlyUsedProfilesKey = new("LogProfileManager.RecentlyUsedProfiles", "");


    // Fields.
    readonly SortedObservableList<LogProfile> pinnedProfiles;
    readonly ObservableList<LogProfile> recentlyUsedProfiles = new(RecentlyUsedProfileCount);


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
            builtInProfileIDs.Add("AndroidDeviceTrace");
            switch (Platform.LinuxDistribution)
            {
                case LinuxDistribution.Fedora:
                    builtInProfileIDs.Add("LinuxRealtimeLog");
                    break;
                default:
                    builtInProfileIDs.Add("LinuxKernelLog");
                    builtInProfileIDs.Add("LinuxSystemLog");
                    break;
            }
        }
        else if (Platform.IsMacOS)
        {
            builtInProfileIDs.Add("AndroidDeviceTrace");
            builtInProfileIDs.Add("AppleDevicesLog");
            builtInProfileIDs.Add("BootedAppleDeviceSimulatorsRealtimeLog");
            builtInProfileIDs.Add("MacOSInstallationLog");
            builtInProfileIDs.Add("MacOSRealtimeLog");
            builtInProfileIDs.Add("SpecificAppleDeviceLog");
            builtInProfileIDs.Add("SpecificAppleDeviceSimulatorLog");
        }
    }


    // Constructor.
    LogProfileManager(IULogViewerApplication app) : base(app)
    { 
        this.EmptyProfile = LogProfile.CreateEmptyBuiltInProfile(app);
        this.pinnedProfiles = new(this.CompareProfiles);
        this.PinnedProfiles = (IReadOnlyList<LogProfile>)ListExtensions.AsReadOnly(this.pinnedProfiles);
        this.RecentlyUsedProfiles = ListExtensions.AsReadOnly(this.recentlyUsedProfiles);
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
        foreach (var profile in this.Profiles.ToArray())
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
    protected override async Task OnInitializeAsync()
    {
        // call base
        await base.OnInitializeAsync();

        // load list of recently used profiles
        var recentlyUsedProfileIdList = this.PersistentState.GetValueOrDefault(recentlyUsedProfilesKey)?.Let(json =>
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var jsonArray = JsonDocument.Parse(json).RootElement;
                    var list = new string?[jsonArray.GetArrayLength()];
                    var i = 0;
                    foreach (var jsonId in jsonArray.EnumerateArray())
                        list[i++] = jsonId.GetString();
                    return list;
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Unable to load list of ID of recently used log profiles");
                }
            }
            return Array.Empty<string?>();
        });
        if (recentlyUsedProfileIdList.IsNotEmpty())
        {
            var idSet = new HashSet<string>();
            foreach (var id in recentlyUsedProfileIdList)
            {
                if (id != null && idSet.Add(id))
                    this.GetProfileOrDefault(id)?.Let(it => this.recentlyUsedProfiles.Add(it));
            }
            this.Logger.LogDebug("{count} recently used log profile(s) found", this.recentlyUsedProfiles.Count);
        }
    }


    /// <inheritdoc/>
    protected override async Task<ICollection<LogProfile>> OnLoadBuiltInProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = new List<LogProfile>();
        foreach (var id in builtInProfileIDs)
        {
            this.Logger.LogDebug("Load '{id}'", id);
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
    /// Get list of recently used log profiles.
    /// </summary>
    public IList<LogProfile> RecentlyUsedProfiles { get; }


    /// <summary>
    /// Remove log profile.
    /// </summary>
    /// <param name="profile">Profile to remove.</param>
    /// <returns>True if profile has neem removed successfully.</returns>
    public bool RemoveProfile(LogProfile profile)
    {
        if (base.RemoveProfile(profile, true))
        {
            if (this.recentlyUsedProfiles.Remove(profile))
                this.SaveRecentlyUsedProfiles();
            return true;
        }
        return false;
    }


    /// <summary>
    /// Reset and clear recently used log profiles.
    /// </summary>
    public void ResetRecentlyUsedProfiles()
    {
        this.VerifyAccess();
        if (this.recentlyUsedProfiles.IsNotEmpty())
        {
            this.recentlyUsedProfiles.Clear();
            this.SaveRecentlyUsedProfiles();
        }
    }


    // Save list of recently used lgo profiles to persistent state.
    void SaveRecentlyUsedProfiles()
    {
        if (this.recentlyUsedProfiles.IsEmpty())
            this.PersistentState.ResetValue(recentlyUsedProfilesKey);
        else
        {
            this.PersistentState.SetValue<string>(recentlyUsedProfilesKey, new MemoryStream().Use(stream =>
            {
                using (var jsonWriter = new Utf8JsonWriter(stream))
                {
                    jsonWriter.WriteStartArray();
                    foreach (var rsProfile in this.recentlyUsedProfiles)
                        jsonWriter.WriteStringValue(rsProfile.Id);
                    jsonWriter.WriteEndArray();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }));
        }
    }
    

    /// <summary>
    /// Set given log profile as recently used one.
    /// </summary>
    /// <param name="profile">Log profile.</param>
    public void SetAsRecentlyUsed(LogProfile profile)
    {
        // check state
        this.VerifyAccess();
        if (!this.Profiles.Contains(profile))
            return;
        if (this.recentlyUsedProfiles.IsNotEmpty() && this.recentlyUsedProfiles[0] == profile)
            return;
        
        // update list
        if (this.recentlyUsedProfiles.Remove(profile))
            this.recentlyUsedProfiles.Insert(0, profile);
        else
        {
            if (this.recentlyUsedProfiles.Count >= RecentlyUsedProfileCount)
                this.recentlyUsedProfiles.RemoveRange(RecentlyUsedProfileCount - 1, this.recentlyUsedProfiles.Count - RecentlyUsedProfileCount + 1);
            this.recentlyUsedProfiles.Insert(0, profile);
        }

        // update persistent state
        this.SaveRecentlyUsedProfiles();
    }
}