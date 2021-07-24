using CarinaStudio.Configuration;
using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application settings.
	/// </summary>
	class Settings : BaseSettings
	{
		/// <summary>
		/// Interval of updating logs for continuous reading.
		/// </summary>
		public static readonly SettingKey<int> ContinuousLogReadingUpdateInterval = new SettingKey<int>(nameof(ContinuousLogReadingUpdateInterval), 100);
		/// <summary>
		/// Application culture.
		/// </summary>
		public static readonly SettingKey<AppCulture> Culture = new SettingKey<AppCulture>(nameof(Culture), AppCulture.System);
		/// <summary>
		/// Ignore case of log text filter.
		/// </summary>
		public static readonly SettingKey<bool> IgnoreCaseOfLogTextFilter = new SettingKey<bool>(nameof(IgnoreCaseOfLogTextFilter), true);
		/// <summary>
		/// Maximum number of logs for continuous logs reading.
		/// </summary>
		public static readonly SettingKey<int> MaxContinuousLogCount = new SettingKey<int>(nameof(MaxContinuousLogCount), 1000000);
		/// <summary>
		/// Select log files immediately when they are needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogFilesWhenNeeded = new SettingKey<bool>(nameof(SelectLogFilesWhenNeeded), false);
		/// <summary>
		/// Select working directory immediately when it is needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectWorkingDirectoryWhenNeeded = new SettingKey<bool>(nameof(SelectWorkingDirectoryWhenNeeded), true);
		/// <summary>
		/// Application theme mode.
		/// </summary>
		public static readonly SettingKey<ThemeMode> ThemeMode = new SettingKey<ThemeMode>(nameof(ThemeMode), ULogViewer.ThemeMode.Dark);
		/// <summary>
		/// Delay of updating log filter after changing related parameters in milliseconds.
		/// </summary>
		public static readonly SettingKey<int> UpdateLogFilterDelay = new SettingKey<int>(nameof(UpdateLogFilterDelay), 500);


		/// <summary>
		/// Maximum value of <see cref="ContinuousLogReadingUpdateInterval"/>.
		/// </summary>
		public const int MaxContinuousLogReadingUpdateInterval = 1000;
		/// <summary>
		/// Maximum value of <see cref="UpdateLogFilterDelay"/>.
		/// </summary>
		public const int MaxUpdateLogFilterDelay = 1500;
		/// <summary>
		/// Minimum value of <see cref="ContinuousLogReadingUpdateInterval"/>.
		/// </summary>
		public const int MinContinuousLogReadingUpdateInterval = 50;
		/// <summary>
		/// Minimum value of <see cref="UpdateLogFilterDelay"/>.
		/// </summary>
		public const int MinUpdateLogFilterDelay = 300;


		/// <summary>
		/// Initialize new <see cref="Settings"/> instance.
		/// </summary>
		public Settings() : base(JsonSettingsSerializer.Default)
		{ }


		// Upgrade.
		protected override void OnUpgrade(int oldVersion)
		{ }


		// Version.
		protected override int Version => 1;
	}
}
