using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Logging;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Manager of <see cref="LogProfile"/>.
/// </summary>
class LogProfileManager : BaseProfileManager<IULogViewerApplication, LogProfile>
{
    // Constants for usage events.
    static class UsageEvents
    { 
        public const string LogProfileCopied = "LogProfile.Copied"; 
        public const string LogProfileCreated = "LogProfile.Created"; 
        public const string LogProfileRemoved = "LogProfile.Removed"; 
        public const string LogProfileSelected = "LogProfile.Selected";
        public const string LogProfileUpdated = "LogProfile.Updated";
    }
    
    
    // Constants for usage properties.
    static class UsageProperties
    {
        public const string DataSourceProvider = "DataSourceProvider";
        public const string HasCooperativeLogAnalysisScriptSet = "HasCooperativeLogAnalysisScriptSet";
        public const string HasLogChart = "HasLogChart";
        public const string IsTemplate = "IsTemplate";
        public const string LogChartSeriesSourceCount = "LogChartSeriesSourceCount";
        public const string LogPatternCount = "LogPatternCount";
        public const string LogProfileId = "Id";
        public const string SourceBuiltInLogProfileId = "SourceBuiltInLogProfile";
        public const string SourceLogProfileId = "SourceLogProfile";
        public const string VisibleLogPropertyCount = "VisibleLogPropertyCount";
    }
    
    
    // Constants.
    const string ScriptLogDataSourceProviderNameForUsageTracking = "Script";
    const int RecentlyUsedProfileCount = 8;


    // Static fields.
    static readonly IList<string> builtInProfileIDs = new List<string>()
    {
        "AndroidDeviceEventLog",
        "AndroidDeviceLog",
        "AndroidFileLog",
        "AndroidKernelLogFile",
        "AndroidProcessMemoryInfo",
        "AndroidSystemMemoryInfo",
        "AndroidTraceFile",
        "ApacheAccessLogFile",
        "ApacheErrorLogFile",
        "AppleDevicesLog",
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
        "RawStandardOutput",
        "RawTcpServer",
        "SpecificAndroidDeviceEventLog",
        "SpecificAndroidDeviceLog",
        "SpecificAndroidDeviceTrace",
        "SpecificAppleDeviceLog",
        "TcpNLog",
        "ULogViewerLog",
        "ULogViewerMemoryLog",
        "WindowsEventLogFiles",
    };
    static LogProfileManager? defaultInstance;
    static readonly SettingKey<string> recentlyUsedProfilesKey = new("LogProfileManager.RecentlyUsedProfiles", "");


    // Fields.
    readonly HashSet<LogProfile> newlyAddedProfiles = new();
    readonly SortedObservableList<LogProfile> pinnedProfiles;
    readonly ObservableList<LogProfile> recentlyUsedProfiles = new(RecentlyUsedProfileCount);


    // Static initializer.
    static LogProfileManager()
    {
        if (Platform.IsWindows)
        {
            builtInProfileIDs.Add("WindowsApplicationEventLogs");
            builtInProfileIDs.Add("WindowsEventLogs");
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
            builtInProfileIDs.Add("BootedAppleDeviceSimulatorsRealtimeLog");
            builtInProfileIDs.Add("MacOSInstallationLog");
            builtInProfileIDs.Add("MacOSRealtimeLog");
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
        this.newlyAddedProfiles.Add(profile);
        try
        {
            base.AddProfile(profile, true);
        }
        catch
        {
            this.newlyAddedProfiles.Remove(profile);
            throw;
        }
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static LogProfileManager Default => defaultInstance ?? throw new InvalidOperationException();


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
        if (profile.IsDataUpgraded)
            this.ScheduleSavingProfile(profile);
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
        var recentlyUsedProfileIdList = this.PersistentState.GetValueOrDefault(recentlyUsedProfilesKey).Let(json =>
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


    /// <inheritdoc/>
    protected override Task OnSaveProfileAsync(LogProfile profile, string fileName)
    {
        if (!this.newlyAddedProfiles.Remove(profile))
        {
            var properties = this.PrepareLogProfileUsageProperties(profile, true);
            this.Application.UsageManager.TrackEvent(UsageEvents.LogProfileUpdated, properties);
        }
        return base.OnSaveProfileAsync(profile, fileName);
    }


    /// <summary>
    /// Get list of pinned <see cref="LogProfile"/>s.
    /// </summary>
    /// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
    public IReadOnlyList<LogProfile> PinnedProfiles { get; }


    /// <summary>
    /// Get all log profiles.
    /// </summary>
    public new IReadOnlyList<LogProfile> Profiles => base.Profiles;


    // Prepare basic properties for tracking log profile related events.
    IDictionary<string, string> PrepareLogProfileUsageProperties(LogProfile logProfile, bool complexProperties) => new Dictionary<string, string>
    {
        [UsageProperties.DataSourceProvider] = logProfile.DataSourceProvider.Let(provider => provider is ScriptLogDataSourceProvider ? ScriptLogDataSourceProviderNameForUsageTracking : provider.Name),
        [UsageProperties.IsTemplate] = logProfile.IsTemplate.ToString(CultureInfo.InvariantCulture),
        [UsageProperties.LogProfileId] = logProfile.IdForUsageTracking,
        [UsageProperties.SourceBuiltInLogProfileId] = logProfile.SourceBuildInLogProfileId ?? "",
    }.Also(it =>
    {
        if (complexProperties)
        {
            var chartSeriesSourceCount = logProfile.LogChartSeriesSources.Count;
            it[UsageProperties.HasCooperativeLogAnalysisScriptSet] = (logProfile.CooperativeLogAnalysisScriptSet is not null).ToString(CultureInfo.InvariantCulture);
            it[UsageProperties.HasLogChart] = (logProfile.LogChartType != LogChartType.None && chartSeriesSourceCount > 0).ToString(CultureInfo.InvariantCulture);
            it[UsageProperties.LogChartSeriesSourceCount] = chartSeriesSourceCount.ToString(CultureInfo.InvariantCulture);
            it[UsageProperties.LogPatternCount] = logProfile.LogPatterns.Count.ToString(CultureInfo.InvariantCulture);
            it[UsageProperties.VisibleLogPropertyCount] = logProfile.VisibleLogProperties.Count.ToString(CultureInfo.InvariantCulture);
        }
    });


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
    /// <returns>True if profile has been removed successfully.</returns>
    public bool RemoveProfile(LogProfile profile)
    {
        this.newlyAddedProfiles.Remove(profile);
        if (base.RemoveProfile(profile, true))
        {
            if (this.recentlyUsedProfiles.Remove(profile))
                this.SaveRecentlyUsedProfiles();
            var properties = this.PrepareLogProfileUsageProperties(profile, true);
            this.Application.UsageManager.TrackEvent(UsageEvents.LogProfileRemoved, properties);
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
            this.PersistentState.SetValue(recentlyUsedProfilesKey, new MemoryStream().Use(stream =>
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
    
    
    /// <summary>
    /// Track usage event for copying of the log profile.
    /// </summary>
    /// <param name="srcLogProfile">The log profile copied from.</param>
    /// <param name="newLogProfile">The new log profile.</param>
    public void TrackLogProfileCopiedEvent(LogProfile srcLogProfile, LogProfile newLogProfile)
    {
        var properties = this.PrepareLogProfileUsageProperties(newLogProfile, true);
        properties[UsageProperties.SourceLogProfileId] = srcLogProfile.IdForUsageTracking;
        this.Application.UsageManager.TrackEvent(UsageEvents.LogProfileCopied, properties);
    }
    
    
    /// <summary>
    /// Track usage event for creation of the log profile.
    /// </summary>
    /// <param name="logProfile">The newly created log profile.</param>
    public void TrackLogProfileCreatedEvent(LogProfile logProfile)
    {
        var properties = this.PrepareLogProfileUsageProperties(logProfile, true);
        this.Application.UsageManager.TrackEvent(UsageEvents.LogProfileCreated, properties);
    }


    /// <summary>
    /// Track usage event for selection of the log profile.
    /// </summary>
    /// <param name="logProfile">The selected log profile.</param>
    public void TrackLogProfileSelectedEvent(LogProfile logProfile)
    {
        var properties = this.PrepareLogProfileUsageProperties(logProfile, false);
        this.Application.UsageManager.TrackEvent(UsageEvents.LogProfileSelected, properties);
    }
}