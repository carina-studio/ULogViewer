using Avalonia.Media;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Application options.
	/// </summary>
	class AppOptions : ViewModel
	{
		// Fields.
		bool isSettingsModified;
		readonly SortedObservableList<LogProfile> logProfiles = new SortedObservableList<LogProfile>(CompareLogProfiles);


		/// <summary>
		/// Initialize new <see cref="AppOptions"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public AppOptions(IApplication app) : base(app)
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
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.ContinuousLogReadingUpdateInterval);
			set => this.Settings.SetValue<int>(ULogViewer.Settings.ContinuousLogReadingUpdateInterval, value);
		}


		/// <summary>
		/// Get or set application culture.
		/// </summary>
		public AppCulture Culture
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.Culture);
			set => this.Settings.SetValue<AppCulture>(ULogViewer.Settings.Culture, value);
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
		/// Get or set whether case of text filter can be ignored or not.
		/// </summary>
		public bool IgnoreCaseOfLogTextFilter
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.IgnoreCaseOfLogTextFilter);
			set => this.Settings.SetValue<bool>(ULogViewer.Settings.IgnoreCaseOfLogTextFilter, value);
		}


		/// <summary>
		/// Get or set initial log profile.
		/// </summary>
		public LogProfile InitialLogProfile
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.InitialLogProfile).Let(it =>
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
					this.Settings.ResetValue(ULogViewer.Settings.InitialLogProfile);
				else
					this.Settings.SetValue<string>(ULogViewer.Settings.InitialLogProfile, it.Id);
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
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.LogFontFamily).Let(it =>
			{
				if (string.IsNullOrEmpty(it))
					return ULogViewer.Settings.DefaultLogFontFamily;
				return it;
			});
			set => this.Settings.SetValue<string>(ULogViewer.Settings.LogFontFamily, value);
		}


		/// <summary>
		/// Get or set font size of log.
		/// </summary>
		public int LogFontSize
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.LogFontSize);
			set => this.Settings.SetValue<int>(ULogViewer.Settings.LogFontSize, value);
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
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.MaxContinuousLogCount);
			set => this.Settings.SetValue<int>(ULogViewer.Settings.MaxContinuousLogCount, value);
		}


		/// <summary>
		/// Get or set maximum number of lines to display for each log.
		/// </summary>
		public int MaxDisplayLineCountForEachLog
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.MaxDisplayLineCountForEachLog);
			set => this.Settings.SetValue<int>(ULogViewer.Settings.MaxDisplayLineCountForEachLog, value);
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
			if (key == ULogViewer.Settings.ContinuousLogReadingUpdateInterval)
				this.OnPropertyChanged(nameof(ContinuousLogReadingUpdateInterval));
			else if (key == ULogViewer.Settings.Culture)
				this.OnPropertyChanged(nameof(Culture));
			else if (key == ULogViewer.Settings.IgnoreCaseOfLogTextFilter)
				this.OnPropertyChanged(nameof(IgnoreCaseOfLogTextFilter));
			else if (key == ULogViewer.Settings.InitialLogProfile)
				this.OnPropertyChanged(nameof(InitialLogProfile));
			else if (key == ULogViewer.Settings.LogFontFamily)
			{
				this.OnPropertyChanged(nameof(LogFontFamily));
				this.SampleLogFontFamily = new FontFamily(this.LogFontFamily);
				this.OnPropertyChanged(nameof(SampleLogFontFamily));
			}
			else if (key == ULogViewer.Settings.LogFontSize)
				this.OnPropertyChanged(nameof(LogFontSize));
			else if (key == ULogViewer.Settings.MaxContinuousLogCount)
				this.OnPropertyChanged(nameof(MaxContinuousLogCount));
			else if (key == ULogViewer.Settings.MaxDisplayLineCountForEachLog)
				this.OnPropertyChanged(nameof(MaxDisplayLineCountForEachLog));
			else if (key == ULogViewer.Settings.SaveMemoryAggressively)
				this.OnPropertyChanged(nameof(SaveMemoryAggressively));
			else if (key == ULogViewer.Settings.SelectLogFilesWhenNeeded)
				this.OnPropertyChanged(nameof(SelectLogFilesWhenNeeded));
			else if (key == ULogViewer.Settings.SelectLogProfileForNewSession)
				this.OnPropertyChanged(nameof(SelectLogProfileForNewSession));
			else if (key == ULogViewer.Settings.SelectWorkingDirectoryWhenNeeded)
				this.OnPropertyChanged(nameof(SelectWorkingDirectoryWhenNeeded));
			else if (key == ULogViewer.Settings.ShowProcessInfo)
				this.OnPropertyChanged(nameof(ShowProcessInfo));
			else if (key == ULogViewer.Settings.ThemeMode)
				this.OnPropertyChanged(nameof(ThemeMode));
			else if (key == ULogViewer.Settings.UpdateLogFilterDelay)
				this.OnPropertyChanged(nameof(UpdateLogFilterDelay));
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
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.SaveMemoryAggressively);
			set => this.Settings.SetValue<bool>(ULogViewer.Settings.SaveMemoryAggressively, value);
		}


		/// <summary>
		/// Get or set whether to select log files immediately when they are needed or not.
		/// </summary>
		public bool SelectLogFilesWhenNeeded
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.SelectLogFilesWhenNeeded);
			set => this.Settings.SetValue<bool>(ULogViewer.Settings.SelectLogFilesWhenNeeded, value);
		}


		/// <summary>
		/// Get or set to select log profile immediately after creating new session.
		/// </summary>
		public bool SelectLogProfileForNewSession
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.SelectLogProfileForNewSession);
			set => this.Settings.SetValue<bool>(ULogViewer.Settings.SelectLogProfileForNewSession, value);
		}


		/// <summary>
		/// Get or set whether to select working directory immediately when they are needed or not.
		/// </summary>
		public bool SelectWorkingDirectoryWhenNeeded
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.SelectWorkingDirectoryWhenNeeded);
			set => this.Settings.SetValue<bool>(ULogViewer.Settings.SelectWorkingDirectoryWhenNeeded, value);
		}


		/// <summary>
		/// Get or set whether process info should be shown on UI or not.
		/// </summary>
		public bool ShowProcessInfo
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.ShowProcessInfo);
			set => this.Settings.SetValue<bool>(ULogViewer.Settings.ShowProcessInfo, value);
		}


		/// <summary>
		/// Get or set application theme mode.
		/// </summary>
		public ThemeMode ThemeMode
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.ThemeMode);
			set => this.Settings.SetValue<ThemeMode>(ULogViewer.Settings.ThemeMode, value);
		}


		/// <summary>
		/// Get or set delay between changing filter conditions and start filtering.
		/// </summary>
		public int UpdateLogFilterDelay
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.UpdateLogFilterDelay);
			set => this.Settings.SetValue<int>(ULogViewer.Settings.UpdateLogFilterDelay, value);
		}
	}
}
