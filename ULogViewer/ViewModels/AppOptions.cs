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
		/// Get or set application culture.
		/// </summary>
		public AppCulture Culture
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.Culture);
			set => this.Settings.SetValue(ULogViewer.Settings.Culture, value);
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
			if (key == ULogViewer.Settings.Culture)
				this.OnPropertyChanged(nameof(Culture));
			else if (key == ULogViewer.Settings.MaxContinuousLogCount)
				this.OnPropertyChanged(nameof(MaxContinuousLogCount));
			else if (key == ULogViewer.Settings.ThemeMode)
				this.OnPropertyChanged(nameof(ThemeMode));
		}


		/// <summary>
		/// Get or set application theme mode.
		/// </summary>
		public ThemeMode ThemeMode
		{
			get => this.Settings.GetValueOrDefault(ULogViewer.Settings.ThemeMode);
			set => this.Settings.SetValue(ULogViewer.Settings.ThemeMode, value);
		}
	}
}
