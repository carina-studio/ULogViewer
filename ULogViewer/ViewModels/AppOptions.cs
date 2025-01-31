using System;
using Avalonia.Media;
using CarinaStudio.AppSuite.Media;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.Logs.Profiles;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Application options.
/// </summary>
class AppOptions : AppSuite.ViewModels.ApplicationOptions
{
	/// <summary>
	/// Information of font family.
	/// </summary>
	public record FontFamilyInfo(string Name, bool IsBuiltIn);


	// Fields.
	bool isSettingsModified;
	readonly SortedObservableList<LogProfile> logProfiles = new(CompareLogProfiles);


	/// <summary>
	/// Initialize new <see cref="AppOptions"/> instance.
	/// </summary>
	public AppOptions()
	{
		// setup properties
		var logFontFamilyName = this.LogFontFamily.Name;
		var patternFontFamilyName = this.PatternFontFamily.Name;
		var scriptEditorFontFamilyName = this.ScriptEditorFontFamily.Name;
		this.logProfiles.Add(LogProfileManager.Default.EmptyProfile);
		this.logProfiles.AddAll(LogProfileManager.Default.Profiles.Where(it => !it.IsTemplate));
		this.LogProfiles = ListExtensions.AsReadOnly(this.logProfiles);
		this.Application.HardwareInfo.TotalPhysicalMemory?.Let(it =>
		{
			var ratio = this.Settings.GetValueOrDefault(SettingKeys.PhysicalMemoryUsagePercentageToStopReadingLogs) / 100.0;
			this.PrivateMemoryUsageToStopReadingLogs = (long)(it * ratio + 0.5);
		});
		this.SampleLogFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(logFontFamilyName)) ?? new FontFamily(logFontFamilyName);
		this.SamplePatternFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(patternFontFamilyName)) ?? new FontFamily(patternFontFamilyName);
		this.SampleScriptEditorFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(scriptEditorFontFamilyName)) ?? new FontFamily(scriptEditorFontFamilyName);

		// add event handlers
		((INotifyCollectionChanged)LogProfileManager.Default.Profiles).CollectionChanged += this.OnLogProfilesChanged;

		// refresh installed text shells
		TextShellManager.Default.RefreshInstalledTextShellsAsync();
	}


	// Compare log profiles.
	static int CompareLogProfiles(LogProfile? x, LogProfile? y)
	{
		if (ReferenceEquals(x, y))
			return 0;
		if (x is null)
			return -1;
		if (y is null)
			return 1;
		if (ReferenceEquals(x, LogProfileManager.Default.EmptyProfile))
			return -1;
		if (ReferenceEquals(y, LogProfileManager.Default.EmptyProfile))
			return 1;
		var result = string.Compare(x.Name, y.Name, true, CultureInfo.InvariantCulture);
		if (result != 0)
			return result;
		// ReSharper disable StringCompareToIsCultureSpecific
		result = x.Id.CompareTo(y.Id);
		// ReSharper restore StringCompareToIsCultureSpecific
		if (result != 0)
			return result;
		return x.GetHashCode() - y.GetHashCode();
	}


	/// <summary>
    /// Let user set the maximum log reading count if files are too large.
    /// </summary>
    public bool ConfirmMaxLogReadingCountForLargeFiles
    {
        get => this.Settings.GetValueOrDefault(SettingKeys.ConfirmMaxLogReadingCountForLargeFiles);
        set => this.Settings.SetValue<bool>(SettingKeys.ConfirmMaxLogReadingCountForLargeFiles, value);
    }


	/// <summary>
	/// Get or set interval of updating logs for continuous reading.
	/// </summary>
	public int ContinuousLogReadingUpdateInterval
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.ContinuousLogReadingUpdateInterval);
		set => this.Settings.SetValue<int>(SettingKeys.ContinuousLogReadingUpdateInterval, value);
	}


	/// <summary>
	/// Get or set default text shell to run commands.
	/// </summary>
	public TextShell DefaultTextShell
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.DefaultTextShell);
		set => this.Settings.SetValue<TextShell>(SettingKeys.DefaultTextShell, value);
	}


	// Dispose.
	protected override void Dispose(bool disposing)
	{
		// remove event handlers
		((INotifyCollectionChanged)LogProfileManager.Default.Profiles).CollectionChanged -= this.OnLogProfilesChanged;

		// save settings
		if (this.isSettingsModified)
		{
			this.Logger.LogDebug("Settings has been modified, save settings");
			_ = (this.Application as App)?.SaveSettingsAsync();
		}

		// call base.
		base.Dispose(disposing);
	}
	
	
	/// <summary>
	/// Enable assistance of log text filter phrase input.
	/// </summary>
	public bool EnableLogTextFilterPhraseInputAssistance
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.EnableLogTextFilterPhraseInputAssistance);
		set => this.Settings.SetValue<bool>(SettingKeys.EnableLogTextFilterPhraseInputAssistance, value);
	}


	/// <summary>
	/// Enable scrolling to latest log automatically after reloading logs.
	/// </summary>
	public bool EnableScrollingToLatestLogAfterReloadingLogs
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs);
		set => this.Settings.SetValue<bool>(SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs, value);
	}


	/// <summary>
    /// Threshold of file size in MB to let user set the maximum log reading count.
    /// </summary>
    public long FileSizeInMBToConfirmMaxLogReadingCount
    {
        get => this.Settings.GetValueOrDefault(SettingKeys.FileSizeInMBToConfirmMaxLogReadingCount);
        set => this.Settings.SetValue<long>(SettingKeys.FileSizeInMBToConfirmMaxLogReadingCount, Math.Max(1, value));
    }


	/// <summary>
	/// Get or set whether case of text filter can be ignored or not.
	/// </summary>
	public bool IgnoreCaseOfLogTextFilter
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.IgnoreCaseOfLogTextFilter);
		set => this.Settings.SetValue<bool>(SettingKeys.IgnoreCaseOfLogTextFilter, value);
	}


	/// <summary>
	/// Get or set initial log profile.
	/// </summary>
	[AllowNull]
	public LogProfile InitialLogProfile
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.InitialLogProfile).Let(it =>
		{
			if (string.IsNullOrEmpty(it))
				return LogProfileManager.Default.EmptyProfile;
			return LogProfileManager.Default.GetProfileOrDefault(it) ?? LogProfileManager.Default.EmptyProfile;
		});
		set
		{
			if (value is null || ReferenceEquals(value, LogProfileManager.Default.EmptyProfile))
				this.Settings.ResetValue(SettingKeys.InitialLogProfile);
			else
				this.Settings.SetValue<string>(SettingKeys.InitialLogProfile, value.Id);
		}
	}


	/// <summary>
	/// Get all installed font families.
	/// </summary>
	public IList<FontFamilyInfo> InstalledFontFamilies { get; } = new List<FontFamilyInfo>().Also(it =>
	{
		// get installed fonts
		foreach (var font in FontManager.Current.SystemFonts)
		{
			var name = font.FamilyNames.FirstOrDefault();
			if (!string.IsNullOrEmpty(name))
				it.Add(new(name, false));
		}

		// sort
		// ReSharper disable StringCompareIsCultureSpecific.1
		var comparison = new Comparison<FontFamilyInfo>((lhs, rhs) => string.Compare(lhs.Name, rhs.Name));
		// ReSharper restore StringCompareIsCultureSpecific.1
		it.Sort(comparison);

		// add built-in fonts
		foreach (var builtInFontFamily in BuiltInFonts.FontFamilies)
		{
			var fontFamilyInfo = new FontFamilyInfo(builtInFontFamily.FamilyNames[0], true);
			var index = it.BinarySearch(fontFamilyInfo, comparison);
			if (index < 0)
				it.Insert(~index, fontFamilyInfo);
			else
				it[index] = fontFamilyInfo;
		}
	}).AsReadOnly();


	/// <summary>
	/// Get or set font family of log.
	/// </summary>
	public FontFamilyInfo LogFontFamily
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.LogFontFamily).Let(it =>
		{
			var familyName = string.IsNullOrEmpty(it)
				? SettingKeys.DefaultLogFontFamily
				: it;
			return this.InstalledFontFamilies.FirstOrDefault(it => it.Name == familyName) ?? new(familyName, false);
		});
		set 
		{
			if (value.Name == SettingKeys.DefaultLogFontFamily)
				this.Settings.ResetValue(SettingKeys.LogFontFamily);
			else
				this.Settings.SetValue<string>(SettingKeys.LogFontFamily, value.Name);
		}
	}


	/// <summary>
	/// Get or set font size of log.
	/// </summary>
	public int LogFontSize
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.LogFontSize);
		set => this.Settings.SetValue<int>(SettingKeys.LogFontSize, value);
	}


	/// <summary>
	/// Get all <see cref="LogProfile"/>s including <see cref="Logs.Profiles.LogProfileManager.EmptyProfile"/>.
	/// </summary>
	public IList<LogProfile> LogProfiles { get; }
	
	
	/// <summary>
	/// Type of log separators.
	/// </summary>
	public LogSeparatorType LogSeparators
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.LogSeparators);
		set => this.Settings.SetValue<LogSeparatorType>(SettingKeys.LogSeparators, value);
	}


	/// <summary>
	/// Get or set maximum number of logs for continuous logs reading.
	/// </summary>
	public int MaxContinuousLogCount
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.MaxContinuousLogCount);
		set => this.Settings.SetValue<int>(SettingKeys.MaxContinuousLogCount, value);
	}


	/// <summary>
	/// Get or set maximum number of lines to display for each log.
	/// </summary>
	[Obsolete]
	public int MaxDisplayLineCountForEachLog
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.MaxDisplayLineCountForEachLog);
		set => this.Settings.SetValue<int>(SettingKeys.MaxDisplayLineCountForEachLog, value);
	}


	/// <summary>
	/// Get maximum value of <see cref="PhysicalMemoryUsagePercentageToStopReadingLogs"/>
	/// </summary>
	public int MaxPhysicalMemoryUsagePercentageToStopReadingLogs => SettingKeys.MaxPhysicalMemoryUsagePercentageToStopReadingLogs;


	/// <summary>
	/// Policy of memory usage.
	/// </summary>
	public MemoryUsagePolicy MemoryUsagePolicy
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy);
		set => this.Settings.SetValue<MemoryUsagePolicy>(SettingKeys.MemoryUsagePolicy, value);
	}
	
	
	/// <summary>
	/// Get minimum value of <see cref="PhysicalMemoryUsagePercentageToStopReadingLogs"/>
	/// </summary>
	public int MinPhysicalMemoryUsagePercentageToStopReadingLogs => SettingKeys.MinPhysicalMemoryUsagePercentageToStopReadingLogs;


	/// <inheritdoc/>
	protected override void OnApplicationStringsUpdated()
	{
		base.OnApplicationStringsUpdated();
		var initLogProfile = this.InitialLogProfile;
		this.logProfiles.Clear();
		this.logProfiles.Add(LogProfileManager.Default.EmptyProfile);
		this.logProfiles.AddAll(LogProfileManager.Default.Profiles.Where(it => !it.IsTemplate));
		this.InitialLogProfile = initLogProfile;
	}


	// Called when list of log profiles changed.
	void OnLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch(e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				this.logProfiles.AddAll(e.NewItems.AsNonNull().Cast<LogProfile>().Where(it => !it.IsTemplate));
				break;
			case NotifyCollectionChangedAction.Remove:
				this.logProfiles.RemoveAll(e.OldItems.AsNonNull().Cast<LogProfile>());
				break;
		}
	}


	// Called when setting changed.
	protected override void OnSettingChanged(SettingChangedEventArgs e)
	{
		base.OnSettingChanged(e);
		var key = e.Key;
		if (key == SettingKeys.ConfirmMaxLogReadingCountForLargeFiles)
			this.OnPropertyChanged(nameof(ConfirmMaxLogReadingCountForLargeFiles));
		else if (key == SettingKeys.ContinuousLogReadingUpdateInterval)
			this.OnPropertyChanged(nameof(ContinuousLogReadingUpdateInterval));
		else if (key == SettingKeys.DefaultTextShell)
			this.OnPropertyChanged(nameof(DefaultTextShell));
		else if (key == SettingKeys.EnableLogTextFilterPhraseInputAssistance)
			this.OnPropertyChanged(nameof(EnableLogTextFilterPhraseInputAssistance));
		else if (key == SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs)
			this.OnPropertyChanged(nameof(EnableScrollingToLatestLogAfterReloadingLogs));
		else if (key == SettingKeys.FileSizeInMBToConfirmMaxLogReadingCount)
			this.OnPropertyChanged(nameof(FileSizeInMBToConfirmMaxLogReadingCount));
		else if (key == SettingKeys.IgnoreCaseOfLogTextFilter)
			this.OnPropertyChanged(nameof(IgnoreCaseOfLogTextFilter));
		else if (key == SettingKeys.InitialLogProfile)
			this.OnPropertyChanged(nameof(InitialLogProfile));
		else if (key == SettingKeys.LogFontFamily)
		{
			var familyName = this.LogFontFamily.Name;
			this.OnPropertyChanged(nameof(LogFontFamily));
			this.SampleLogFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(familyName)) ?? new FontFamily(familyName);
			this.OnPropertyChanged(nameof(SampleLogFontFamily));
		}
		else if (key == SettingKeys.LogFontSize)
			this.OnPropertyChanged(nameof(LogFontSize));
		else if (key == SettingKeys.LogSeparators)
			this.OnPropertyChanged(nameof(LogSeparators));
		else if (key == SettingKeys.MaxContinuousLogCount)
			this.OnPropertyChanged(nameof(MaxContinuousLogCount));
#pragma warning disable CS0612
		else if (key == SettingKeys.MaxDisplayLineCountForEachLog)
			this.OnPropertyChanged(nameof(MaxDisplayLineCountForEachLog));
#pragma warning restore CS0612
		else if (key == SettingKeys.MemoryUsagePolicy)
			this.OnPropertyChanged(nameof(MemoryUsagePolicy));
		else if (key == SettingKeys.PatternFontFamily)
		{
			var familyName = this.PatternFontFamily.Name;
			this.OnPropertyChanged(nameof(PatternFontFamily));
			this.SamplePatternFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(familyName)) ?? new FontFamily(familyName);
			this.OnPropertyChanged(nameof(SamplePatternFontFamily));
		}
		else if (key == SettingKeys.PhysicalMemoryUsagePercentageToStopReadingLogs)
		{
			this.OnPropertyChanged(nameof(PhysicalMemoryUsagePercentageToStopReadingLogs));
			this.Application.HardwareInfo.TotalPhysicalMemory?.Let(it =>
			{
				this.PrivateMemoryUsageToStopReadingLogs = (long)(it * ((long) e.Value / 100.0) + 0.5);
				this.OnPropertyChanged(nameof(PrivateMemoryUsageToStopReadingLogs));
			});
		}
		else if (key == SettingKeys.ResetLogAnalysisRuleSetsAfterSettingLogProfile)
			this.OnPropertyChanged(nameof(ResetLogAnalysisRuleSetsAfterSettingLogProfile));
		else if (key == SettingKeys.ScriptEditorFontFamily)
		{
			var familyName = this.ScriptEditorFontFamily.Name;
			this.OnPropertyChanged(nameof(ScriptEditorFontFamily));
			this.SampleScriptEditorFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(familyName)) ?? new FontFamily(familyName);
			this.OnPropertyChanged(nameof(SampleScriptEditorFontFamily));
		}
		else if (key == SettingKeys.ScriptEditorFontSize)
			this.OnPropertyChanged(nameof(ScriptEditorFontSize));
		else if (key == SettingKeys.SelectCommandWhenNeeded)
			this.OnPropertyChanged(nameof(SelectCommandWhenNeeded));
		else if (key == SettingKeys.SelectIPEndPointWhenNeeded)
			this.OnPropertyChanged(nameof(SelectIPEndPointWhenNeeded));
		else if (key == SettingKeys.SelectLogFilesWhenNeeded)
			this.OnPropertyChanged(nameof(SelectLogFilesWhenNeeded));
		else if (key == SettingKeys.SelectLogProfileForNewSession)
			this.OnPropertyChanged(nameof(SelectLogProfileForNewSession));
		else if (key == SettingKeys.SelectLogReadingPreconditionForFiles)
			this.OnPropertyChanged(nameof(SelectLogReadingPreconditionForFiles));
		else if (key == SettingKeys.SelectProcessIdWhenNeeded)
			this.OnPropertyChanged(nameof(SelectProcessIdWhenNeeded));
		else if (key == SettingKeys.SelectProcessNameWhenNeeded)
			this.OnPropertyChanged(nameof(SelectProcessNameWhenNeeded));
		else if (key == SettingKeys.SelectUriWhenNeeded)
			this.OnPropertyChanged(nameof(SelectUriWhenNeeded));
		else if (key == SettingKeys.SelectWorkingDirectoryWhenNeeded)
			this.OnPropertyChanged(nameof(SelectWorkingDirectoryWhenNeeded));
		else if (key == SettingKeys.ShowHelpButtonOnLogTextFilter)
			this.OnPropertyChanged(nameof(ShowHelpButtonOnLogTextFilter));
		else if (key == SettingKeys.ShowLogChartPanelIfDefined)
			this.OnPropertyChanged(nameof(ShowLogChartPanelIfDefined));
		else if (key == AppSuite.SettingKeys.ShowProcessInfo)
			this.OnPropertyChanged(nameof(ShowProcessInfo));
		else if (key == SettingKeys.SwitchToMarkedLogsPanelAfterMarkingLogs)
			this.OnPropertyChanged(nameof(SwitchToMarkedLogsPanelAfterMarkingLogs));
		else if (key == SettingKeys.UpdateLogFilterDelay)
			this.OnPropertyChanged(nameof(UpdateLogFilterDelay));
		else
			return;
		this.isSettingsModified = true;
	}


	/// <summary>
	/// Get or set font family of pattern.
	/// </summary>
	public FontFamilyInfo PatternFontFamily
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.PatternFontFamily).Let(it =>
		{
			var familyName = string.IsNullOrEmpty(it)
				? SettingKeys.DefaultPatternFontFamily
				: it;
			return this.InstalledFontFamilies.FirstOrDefault(it => it.Name == familyName) ?? new(familyName, false);
		});
		set 
		{
			if (value.Name == SettingKeys.DefaultPatternFontFamily)
				this.Settings.ResetValue(SettingKeys.PatternFontFamily);
			else
				this.Settings.SetValue<string>(SettingKeys.PatternFontFamily, value.Name);
		}
	}


	/// <summary>
	/// Percentage of physical memory usage by application to stop reading logs.
	/// </summary>
	public long PhysicalMemoryUsagePercentageToStopReadingLogs
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.PhysicalMemoryUsagePercentageToStopReadingLogs);
		set
		{
			if (value < SettingKeys.MinPhysicalMemoryUsagePercentageToStopReadingLogs)
				value = SettingKeys.MinPhysicalMemoryUsagePercentageToStopReadingLogs;
			else if (value > SettingKeys.MaxPhysicalMemoryUsagePercentageToStopReadingLogs)
				value = SettingKeys.MaxPhysicalMemoryUsagePercentageToStopReadingLogs;
			this.Settings.SetValue<long>(SettingKeys.PhysicalMemoryUsagePercentageToStopReadingLogs, value);
		}
	}
	
	
	/// <summary>
	/// Process memory usage to stop reading logs.
	/// </summary>
	public long PrivateMemoryUsageToStopReadingLogs { get; private set; }


	/// <summary>
	/// Reset all log analysis rule sets after setting log profile.
	/// </summary>
	public bool ResetLogAnalysisRuleSetsAfterSettingLogProfile
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.ResetLogAnalysisRuleSetsAfterSettingLogProfile);
		set => this.Settings.SetValue<bool>(SettingKeys.ResetLogAnalysisRuleSetsAfterSettingLogProfile, value);
	}


	/// <summary>
	/// Get <see cref="FontFamily"/> for sample log text.
	/// </summary>
	public FontFamily SampleLogFontFamily { get; private set; }


	/// <summary>
	/// Get <see cref="FontFamily"/> for sample pattern text.
	/// </summary>
	public FontFamily SamplePatternFontFamily { get; private set; }


	/// <summary>
	/// Get <see cref="FontFamily"/> for sample script.
	/// </summary>
	public FontFamily SampleScriptEditorFontFamily { get; private set; }


	/// <summary>
	/// Get or set font family of script editor.
	/// </summary>
	public FontFamilyInfo ScriptEditorFontFamily
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.ScriptEditorFontFamily).Let(it =>
		{
			var familyName = string.IsNullOrEmpty(it)
				? SettingKeys.DefaultScriptEditorFontFamily
				: it;
			return this.InstalledFontFamilies.FirstOrDefault(it => it.Name == familyName) ?? new(familyName, false);
		});
		set 
		{
			if (value.Name == SettingKeys.DefaultScriptEditorFontFamily)
				this.Settings.ResetValue(SettingKeys.ScriptEditorFontFamily);
			else
				this.Settings.SetValue<string>(SettingKeys.ScriptEditorFontFamily, value.Name);
		}
	}


	/// <summary>
	/// Get or set font size of script editor.
	/// </summary>
	public int ScriptEditorFontSize
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.ScriptEditorFontSize);
		set => this.Settings.SetValue<int>(SettingKeys.ScriptEditorFontSize, value);
	}
	
	
	/// <summary>
	/// Get or set whether to select command immediately when they are needed or not.
	/// </summary>
	public bool SelectCommandWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectCommandWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectCommandWhenNeeded, value);
	}


	/// <summary>
	/// Get or set whether to select IP endpoint immediately when they are needed or not.
	/// </summary>
	public bool SelectIPEndPointWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectIPEndPointWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectIPEndPointWhenNeeded, value);
	}


	/// <summary>
	/// Get or set whether to select log files immediately when they are needed or not.
	/// </summary>
	public bool SelectLogFilesWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectLogFilesWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectLogFilesWhenNeeded, value);
	}


	/// <summary>
	/// Get or set to select log profile immediately after creating new session.
	/// </summary>
	public bool SelectLogProfileForNewSession
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectLogProfileForNewSession);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectLogProfileForNewSession, value);
	}


	/// <summary>
	/// Select precondition before reading logs from files.
	/// </summary>
	public bool SelectLogReadingPreconditionForFiles
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectLogReadingPreconditionForFiles);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectLogReadingPreconditionForFiles, value);
	}
	
	
	/// <summary>
	/// Get or set whether to select process ID immediately when they are needed or not.
	/// </summary>
	public bool SelectProcessIdWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectProcessIdWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectProcessIdWhenNeeded, value);
	}
	
	
	/// <summary>
	/// Get or set whether to select process name immediately when they are needed or not.
	/// </summary>
	public bool SelectProcessNameWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectProcessNameWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectProcessNameWhenNeeded, value);
	}


	/// <summary>
	/// Get or set whether to select URI immediately when they are needed or not.
	/// </summary>
	public bool SelectUriWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectUriWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectUriWhenNeeded, value);
	}


	/// <summary>
	/// Get or set whether to select working directory immediately when they are needed or not.
	/// </summary>
	public bool SelectWorkingDirectoryWhenNeeded
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SelectWorkingDirectoryWhenNeeded);
		set => this.Settings.SetValue<bool>(SettingKeys.SelectWorkingDirectoryWhenNeeded, value);
	}


	/// <summary>
	/// Get or set visibility of help button on log text filter.
	/// </summary>
	public bool ShowHelpButtonOnLogTextFilter
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.ShowHelpButtonOnLogTextFilter);
		set => this.Settings.SetValue<bool>(SettingKeys.ShowHelpButtonOnLogTextFilter, value);
	}
	
	
	/// <summary>
	/// Show panel of log chart if log chart is defined.
	/// </summary>
	public bool ShowLogChartPanelIfDefined
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.ShowLogChartPanelIfDefined);
		set => this.Settings.SetValue<bool>(SettingKeys.ShowLogChartPanelIfDefined, value);
	}


	/// <summary>
	/// Get or set whether process info should be shown on UI or not.
	/// </summary>
	public bool ShowProcessInfo
	{
		get => this.Settings.GetValueOrDefault(AppSuite.SettingKeys.ShowProcessInfo);
		set => this.Settings.SetValue<bool>(AppSuite.SettingKeys.ShowProcessInfo, value);
	}
	
	
	/// <summary>
	/// Switch to panel of marked logs automatically after marking logs.
	/// </summary>
	public bool SwitchToMarkedLogsPanelAfterMarkingLogs
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.SwitchToMarkedLogsPanelAfterMarkingLogs);
		set => this.Settings.SetValue<bool>(SettingKeys.SwitchToMarkedLogsPanelAfterMarkingLogs, value);
	}


	/// <summary>
	/// Get or set delay between changing filter conditions and start filtering.
	/// </summary>
	public int UpdateLogFilterDelay
	{
		get => this.Settings.GetValueOrDefault(SettingKeys.UpdateLogFilterDelay);
		set => this.Settings.SetValue<int>(SettingKeys.UpdateLogFilterDelay, value);
	}
}
