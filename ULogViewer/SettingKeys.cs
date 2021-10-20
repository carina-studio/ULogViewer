using Avalonia.Media;
using CarinaStudio.Configuration;
using System;
using System.Runtime.InteropServices;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Setting keys.
	/// </summary>
	static class SettingKeys
	{
		/// <summary>
		/// Interval of updating logs for continuous reading.
		/// </summary>
		public static readonly SettingKey<int> ContinuousLogReadingUpdateInterval = new SettingKey<int>(nameof(ContinuousLogReadingUpdateInterval), 100);
		/// <summary>
		/// Enable scrolling to latest log automatically after reloading logs.
		/// </summary>
		public static readonly SettingKey<bool> EnableScrollingToLatestLogAfterReloadingLogs = new SettingKey<bool>(nameof(EnableScrollingToLatestLogAfterReloadingLogs), true);
		/// <summary>
		/// Ignore case of log text filter.
		/// </summary>
		public static readonly SettingKey<bool> IgnoreCaseOfLogTextFilter = new SettingKey<bool>(nameof(IgnoreCaseOfLogTextFilter), true);
		/// <summary>
		/// ID of initial log profile.
		/// </summary>
		public static readonly SettingKey<string> InitialLogProfile = new SettingKey<string>(nameof(InitialLogProfile), "");
		/// <summary>
		/// Font family of log.
		/// </summary>
		public static readonly SettingKey<string> LogFontFamily = new SettingKey<string>(nameof(LogFontFamily), "");
		/// <summary>
		/// Font size of log.
		/// </summary>
		public static readonly SettingKey<int> LogFontSize = new SettingKey<int>(nameof(LogFontSize), 14);
		/// <summary>
		/// Maximum number of logs for continuous logs reading.
		/// </summary>
		public static readonly SettingKey<int> MaxContinuousLogCount = new SettingKey<int>(nameof(MaxContinuousLogCount), 1000000);
		/// <summary>
		/// Maximum line count to display for each log.
		/// </summary>
		public static readonly SettingKey<int> MaxDisplayLineCountForEachLog = new SettingKey<int>(nameof(MaxDisplayLineCountForEachLog), 5);
		/// <summary>
		/// Keep memory usage as low as possible.
		/// </summary>
		public static readonly SettingKey<bool> SaveMemoryAggressively = new SettingKey<bool>(nameof(SaveMemoryAggressively), false);
		/// <summary>
		/// Select log files immediately when they are needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogFilesWhenNeeded = new SettingKey<bool>(nameof(SelectLogFilesWhenNeeded), false);
		/// <summary>
		/// Select log profile immediately after creating new session.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogProfileForNewSession = new SettingKey<bool>(nameof(SelectLogProfileForNewSession), true);
		/// <summary>
		/// Select working directory immediately when it is needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectWorkingDirectoryWhenNeeded = new SettingKey<bool>(nameof(SelectWorkingDirectoryWhenNeeded), true);
		/// <summary>
		/// Show process info on UI or not.
		/// </summary>
		public static readonly SettingKey<bool> ShowProcessInfo = new SettingKey<bool>(nameof(ShowProcessInfo), false);
		/// <summary>
		/// Delay of updating log filter after changing related parameters in milliseconds.
		/// </summary>
		public static readonly SettingKey<int> UpdateLogFilterDelay = new SettingKey<int>(nameof(UpdateLogFilterDelay), 500);
		/// <summary>
		/// Use system accent color or not.
		/// </summary>
		public static readonly SettingKey<bool> UseSystemAccentColor = new SettingKey<bool>(nameof(UseSystemAccentColor), true);


		/// <summary>
		/// Maximum value of <see cref="ContinuousLogReadingUpdateInterval"/>.
		/// </summary>
		public const int MaxContinuousLogReadingUpdateInterval = 1000;
		/// <summary>
		/// Maximum value of <see cref="LogFontSize"/>.
		/// </summary>
		public const int MaxLogFontSize = 30;
		/// <summary>
		/// Maximum value of <see cref="UpdateLogFilterDelay"/>.
		/// </summary>
		public const int MaxUpdateLogFilterDelay = 1500;
		/// <summary>
		/// Minimum value of <see cref="ContinuousLogReadingUpdateInterval"/>.
		/// </summary>
		public const int MinContinuousLogReadingUpdateInterval = 50;
		/// <summary>
		/// Maximum value of <see cref="LogFontSize"/>.
		/// </summary>
		public const int MinLogFontSize = 10;
		/// <summary>
		/// Minimum value of <see cref="UpdateLogFilterDelay"/>.
		/// </summary>
		public const int MinUpdateLogFilterDelay = 300;


		/// <summary>
		/// Default font family of log.
		/// </summary>
		public static string DefaultLogFontFamily { get; } = Global.Run(() =>
		{
			var targetFontFamily = Global.Run(() =>
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					return "";
				return "Consolas";
			});
			if (!string.IsNullOrEmpty(targetFontFamily))
			{
				foreach (var name in FontManager.Current.GetInstalledFontFamilyNames())
				{
					if (name == targetFontFamily)
						return targetFontFamily;
				}
			}
			return FontManager.Current.DefaultFontFamilyName;
		});
	}
}
