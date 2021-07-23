using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.ViewModels;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Application options.
	/// </summary>
	class AppOptions : ViewModel
	{
		/// <summary>
		/// Initialize new <see cref="AppOptions"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public AppOptions(IApplication app) : base(app)
		{
			//
		}


		/// <summary>
		/// Get or set interval of updating logs for continuous reading.
		/// </summary>
		public int ContinuousLogReadingUpdateInterval
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.ContinuousLogReadingUpdateInterval);
			set => this.Settings.SetValue(ULogViewer.Settings.ContinuousLogReadingUpdateInterval, value);
		}


		/// <summary>
		/// Get or set application culture.
		/// </summary>
		public AppCulture Culture
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.Culture);
			set => this.Settings.SetValue(ULogViewer.Settings.Culture, value);
		}


		/// <summary>
		/// Get or set whether case of text filter can be ignored or not.
		/// </summary>
		public bool IgnoreCaseOfLogTextFilter
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.IgnoreCaseOfLogTextFilter);
			set => this.Settings.SetValue(ULogViewer.Settings.IgnoreCaseOfLogTextFilter, value);
		}


		/// <summary>
		/// Get or set maximum number of logs for continuous logs reading.
		/// </summary>
		public int MaxContinuousLogCount
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.MaxContinuousLogCount);
			set => this.Settings.SetValue(ULogViewer.Settings.MaxContinuousLogCount, value);
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
			else if (key == ULogViewer.Settings.MaxContinuousLogCount)
				this.OnPropertyChanged(nameof(MaxContinuousLogCount));
			else if (key == ULogViewer.Settings.SelectLogFilesWhenNeeded)
				this.OnPropertyChanged(nameof(SelectLogFilesWhenNeeded));
			else if (key == ULogViewer.Settings.SelectWorkingDirectoryWhenNeeded)
				this.OnPropertyChanged(nameof(SelectWorkingDirectoryWhenNeeded));
			else if (key == ULogViewer.Settings.ThemeMode)
				this.OnPropertyChanged(nameof(ThemeMode));
			else if (key == ULogViewer.Settings.UpdateLogFilterDelay)
				this.OnPropertyChanged(nameof(UpdateLogFilterDelay));
		}


		/// <summary>
		/// Get or set whether to select log files immediately when they are needed or not.
		/// </summary>
		public bool SelectLogFilesWhenNeeded
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.SelectLogFilesWhenNeeded);
			set => this.Settings.SetValue(ULogViewer.Settings.SelectLogFilesWhenNeeded, value);
		}


		/// <summary>
		/// Get or set whether to select working directory immediately when they are needed or not.
		/// </summary>
		public bool SelectWorkingDirectoryWhenNeeded
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.SelectWorkingDirectoryWhenNeeded);
			set => this.Settings.SetValue(ULogViewer.Settings.SelectWorkingDirectoryWhenNeeded, value);
		}


		/// <summary>
		/// Get or set application theme mode.
		/// </summary>
		public ThemeMode ThemeMode
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.ThemeMode);
			set => this.Settings.SetValue(ULogViewer.Settings.ThemeMode, value);
		}


		/// <summary>
		/// Get or set delay between changing filter conditions and start filtering.
		/// </summary>
		public int UpdateLogFilterDelay
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.UpdateLogFilterDelay);
			set => this.Settings.SetValue(ULogViewer.Settings.UpdateLogFilterDelay, value);
		}
	}
}
