﻿using Avalonia.Media;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Logs.Profiles;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Application options.
	/// </summary>
	class AppOptions : AppSuite.ViewModels.ApplicationOptions
	{
		// Fields.
		bool isSettingsModified;
		readonly SortedObservableList<LogProfile> logProfiles = new SortedObservableList<LogProfile>(CompareLogProfiles);


		/// <summary>
		/// Initialize new <see cref="AppOptions"/> instance.
		/// </summary>
		public AppOptions() : base()
		{
			// setup properties
			this.logProfiles.Add(Logs.Profiles.LogProfiles.EmptyProfile);
			this.logProfiles.AddAll(Logs.Profiles.LogProfiles.All);
			this.LogProfiles = this.logProfiles.AsReadOnly();
			this.SampleLogFontFamily = new FontFamily(this.LogFontFamily);

			// add event handlers
			((INotifyCollectionChanged)Logs.Profiles.LogProfiles.All).CollectionChanged += this.OnLogProfilesChanged;
		}


		// Compare log profiles.
		static int CompareLogProfiles(LogProfile? x, LogProfile? y)
		{
			if (object.ReferenceEquals(x, y))
				return 0;
			if (x == null)
				return -1;
			if (y == null)
				return 1;
			if (x == Logs.Profiles.LogProfiles.EmptyProfile)
				return -1;
			if (y == Logs.Profiles.LogProfiles.EmptyProfile)
				return 1;
			var result = x.Name.CompareTo(y.Name);
			if (result != 0)
				return result;
			result = x.Id.CompareTo(y.Id);
			if (result != 0)
				return result;
			return x.GetHashCode() - y.GetHashCode();
		}


		/// <summary>
		/// Get or set interval of updating logs for continuous reading.
		/// </summary>
		public int ContinuousLogReadingUpdateInterval
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.ContinuousLogReadingUpdateInterval);
			set => this.Settings.SetValue<int>(SettingKeys.ContinuousLogReadingUpdateInterval, value);
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// remove event handlers
			((INotifyCollectionChanged)Logs.Profiles.LogProfiles.All).CollectionChanged -= this.OnLogProfilesChanged;

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
		/// Enable scrolling to latest log automatically after reloading logs.
		/// </summary>
		public bool EnableScrollingToLatestLogAfterReloadingLogs
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs);
			set => this.Settings.SetValue<bool>(SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs, value);
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
		public LogProfile InitialLogProfile
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.InitialLogProfile).Let(it =>
			{
				if (string.IsNullOrEmpty(it))
					return Logs.Profiles.LogProfiles.EmptyProfile;
				if (Logs.Profiles.LogProfiles.TryFindProfileById(it, out var profile))
					return profile.AsNonNull();
				return Logs.Profiles.LogProfiles.EmptyProfile;
			});
			set => value.Let(it =>
			{
				if (it == Logs.Profiles.LogProfiles.EmptyProfile)
					this.Settings.ResetValue(SettingKeys.InitialLogProfile);
				else
					this.Settings.SetValue<string>(SettingKeys.InitialLogProfile, it.Id);
			});
		}


		/// <summary>
		/// Get all installed font families.
		/// </summary>
		public IList<string> InstalledFontFamilies { get; } = new List<string>(FontManager.Current.GetInstalledFontFamilyNames()).Also(it =>
		{
			it.Sort();
		}).AsReadOnly();


		/// <summary>
		/// Get or set font family of log.
		/// </summary>
		public string LogFontFamily
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.LogFontFamily).Let(it =>
			{
				if (string.IsNullOrEmpty(it))
					return SettingKeys.DefaultLogFontFamily;
				return it;
			});
			set => this.Settings.SetValue<string>(SettingKeys.LogFontFamily, value);
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
		/// Get all <see cref="LogProfile"/>s including <see cref="Logs.Profiles.LogProfiles.EmptyProfile"/>.
		/// </summary>
		public IList<LogProfile> LogProfiles { get; }


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
		public int MaxDisplayLineCountForEachLog
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.MaxDisplayLineCountForEachLog);
			set => this.Settings.SetValue<int>(SettingKeys.MaxDisplayLineCountForEachLog, value);
		}


		// Called when list of log profiles changed.
		void OnLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					this.logProfiles.AddAll(e.NewItems.AsNonNull().Cast<LogProfile>());
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
			if (key == SettingKeys.ContinuousLogReadingUpdateInterval)
				this.OnPropertyChanged(nameof(ContinuousLogReadingUpdateInterval));
			else if (key == SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs)
				this.OnPropertyChanged(nameof(EnableScrollingToLatestLogAfterReloadingLogs));
			else if (key == SettingKeys.IgnoreCaseOfLogTextFilter)
				this.OnPropertyChanged(nameof(IgnoreCaseOfLogTextFilter));
			else if (key == SettingKeys.InitialLogProfile)
				this.OnPropertyChanged(nameof(InitialLogProfile));
			else if (key == SettingKeys.LogFontFamily)
			{
				this.OnPropertyChanged(nameof(LogFontFamily));
				this.SampleLogFontFamily = new FontFamily(this.LogFontFamily);
				this.OnPropertyChanged(nameof(SampleLogFontFamily));
			}
			else if (key == SettingKeys.LogFontSize)
				this.OnPropertyChanged(nameof(LogFontSize));
			else if (key == SettingKeys.MaxContinuousLogCount)
				this.OnPropertyChanged(nameof(MaxContinuousLogCount));
			else if (key == SettingKeys.MaxDisplayLineCountForEachLog)
				this.OnPropertyChanged(nameof(MaxDisplayLineCountForEachLog));
			else if (key == SettingKeys.SaveMemoryAggressively)
				this.OnPropertyChanged(nameof(SaveMemoryAggressively));
			else if (key == SettingKeys.SelectIPEndPointWhenNeeded)
				this.OnPropertyChanged(nameof(SelectIPEndPointWhenNeeded));
			else if (key == SettingKeys.SelectLogFilesWhenNeeded)
				this.OnPropertyChanged(nameof(SelectLogFilesWhenNeeded));
			else if (key == SettingKeys.SelectLogProfileForNewSession)
				this.OnPropertyChanged(nameof(SelectLogProfileForNewSession));
			else if (key == SettingKeys.SelectUriWhenNeeded)
				this.OnPropertyChanged(nameof(SelectUriWhenNeeded));
			else if (key == SettingKeys.SelectWorkingDirectoryWhenNeeded)
				this.OnPropertyChanged(nameof(SelectWorkingDirectoryWhenNeeded));
			else if (key == SettingKeys.ShowProcessInfo)
				this.OnPropertyChanged(nameof(ShowProcessInfo));
			else if (key == SettingKeys.UpdateLogFilterDelay)
				this.OnPropertyChanged(nameof(UpdateLogFilterDelay));
			else if (key == SettingKeys.UseSystemAccentColor)
				this.OnPropertyChanged(nameof(UseSystemAccentColor));
			else
				return;
			this.isSettingsModified = true;
		}


		/// <summary>
		/// Get <see cref="FontFamily"/> for sample log text.
		/// </summary>
		public FontFamily SampleLogFontFamily { get; private set; }


		/// <summary>
		/// Get or set whether application need to keep memory usage as low as possible.
		/// </summary>
		public bool SaveMemoryAggressively
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.SaveMemoryAggressively);
			set => this.Settings.SetValue<bool>(SettingKeys.SaveMemoryAggressively, value);
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
		/// Get or set whether process info should be shown on UI or not.
		/// </summary>
		public bool ShowProcessInfo
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.ShowProcessInfo);
			set => this.Settings.SetValue<bool>(SettingKeys.ShowProcessInfo, value);
		}


		/// <summary>
		/// Get or set delay between changing filter conditions and start filtering.
		/// </summary>
		public int UpdateLogFilterDelay
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.UpdateLogFilterDelay);
			set => this.Settings.SetValue<int>(SettingKeys.UpdateLogFilterDelay, value);
		}


		/// <summary>
		/// Get or set whether to use system accent color if possible or not.
		/// </summary>
		public bool UseSystemAccentColor
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.UseSystemAccentColor);
			set => this.Settings.SetValue<bool>(SettingKeys.UseSystemAccentColor, value);
		}
	}
}
