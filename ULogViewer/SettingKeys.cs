using System.Collections.Generic;
using Avalonia.Media;
using CarinaStudio.Configuration;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Setting keys.
	/// </summary>
	static class SettingKeys
	{
		/// <summary>
		/// Accuracy of filtering logs by specific property of selected log.
		/// </summary>
		public static readonly SettingKey<Accuracy> AccuracyOfFilteringBySelectedLogProperty = new(nameof(AccuracyOfFilteringBySelectedLogProperty), Accuracy.Normal);
		/// <summary>
		/// Interval of updating logs for continuous reading.
		/// </summary>
		public static readonly SettingKey<int> ContinuousLogReadingUpdateInterval = new(nameof(ContinuousLogReadingUpdateInterval), 100);
		/// <summary>
		/// Enable scrolling to latest log automatically after reloading logs.
		/// </summary>
		public static readonly SettingKey<bool> EnableScrollingToLatestLogAfterReloadingLogs = new(nameof(EnableScrollingToLatestLogAfterReloadingLogs), true);
		/// <summary>
		/// Ignore case of log text filter.
		/// </summary>
		public static readonly SettingKey<bool> IgnoreCaseOfLogTextFilter = new(nameof(IgnoreCaseOfLogTextFilter), true);
		/// <summary>
		/// ID of initial log profile.
		/// </summary>
		public static readonly SettingKey<string> InitialLogProfile = new(nameof(InitialLogProfile), "");
		/// <summary>
		/// Font family of log.
		/// </summary>
		public static readonly SettingKey<string> LogFontFamily = new(nameof(LogFontFamily), "");
		/// <summary>
		/// Font size of log.
		/// </summary>
		public static readonly SettingKey<int> LogFontSize = new(nameof(LogFontSize), Platform.IsMacOS ? 13 : 14);
		/// <summary>
		/// Maximum number of logs for continuous logs reading.
		/// </summary>
		public static readonly SettingKey<int> MaxContinuousLogCount = new(nameof(MaxContinuousLogCount), 1000000);
		/// <summary>
		/// Maximum line count to display for each log.
		/// </summary>
		public static readonly SettingKey<int> MaxDisplayLineCountForEachLog = new(nameof(MaxDisplayLineCountForEachLog), 5);
		/// <summary>
		/// Policy of memory usage.
		/// </summary>
		public static readonly SettingKey<MemoryUsagePolicy> MemoryUsagePolicy = new(nameof(MemoryUsagePolicy), ULogViewer.MemoryUsagePolicy.Balance);
		/// <summary>
		/// Reset all log analysis rule sets after setting log profile.
		/// </summary>
		public static readonly SettingKey<bool> ResetLogAnalysisRuleSetsAfterSettingLogProfile = new(nameof(ResetLogAnalysisRuleSetsAfterSettingLogProfile), true);
		/// <summary>
		/// Font family of script editor.
		/// </summary>
		public static readonly SettingKey<string> ScriptEditorFontFamily = new(nameof(ScriptEditorFontFamily), "");
		/// <summary>
		/// Font size of script editor.
		/// </summary>
		public static readonly SettingKey<int> ScriptEditorFontSize = new(nameof(ScriptEditorFontSize), Platform.IsMacOS ? 13 : 14);
		/// <summary>
		/// Select IP endpoint immediately when they are needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectIPEndPointWhenNeeded = new(nameof(SelectIPEndPointWhenNeeded), true);
		/// <summary>
		/// Select log files immediately when they are needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogFilesWhenNeeded = new(nameof(SelectLogFilesWhenNeeded), false);
		/// <summary>
		/// Select log profile immediately after creating new session.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogProfileForNewSession = new(nameof(SelectLogProfileForNewSession), true);
		/// <summary>
		/// Select precondition before reading logs from files.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogReadingPreconditionForFiles = new(nameof(SelectLogReadingPreconditionForFiles), true);
		/// <summary>
		/// Select URI immediately when it is needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectUriWhenNeeded = new(nameof(SelectUriWhenNeeded), true);
		/// <summary>
		/// Select working directory immediately when it is needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectWorkingDirectoryWhenNeeded = new(nameof(SelectWorkingDirectoryWhenNeeded), true);
		/// <summary>
		/// Delay of updating log filter after changing related parameters in milliseconds.
		/// </summary>
		public static readonly SettingKey<int> UpdateLogFilterDelay = new(nameof(UpdateLogFilterDelay), 500);
		/// <summary>
		/// Use system accent color or not.
		/// </summary>
		public static readonly SettingKey<bool> UseSystemAccentColor = new(nameof(UseSystemAccentColor), true);


		/// <summary>
		/// Maximum value of <see cref="ContinuousLogReadingUpdateInterval"/>.
		/// </summary>
		public const int MaxContinuousLogReadingUpdateInterval = 1000;
		/// <summary>
		/// Maximum value of <see cref="LogFontSize"/>.
		/// </summary>
		public const int MaxLogFontSize = 30;
		/// <summary>
		/// Maximum value of <see cref="ScriptEditorFontSize"/>.
		/// </summary>
		public const int MaxScriptEditorFontSize = 30;
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
		/// Minimum value of <see cref="ScriptEditorFontSize"/>.
		/// </summary>
		public const int MinScriptEditorFontSize = 10;
		/// <summary>
		/// Minimum value of <see cref="UpdateLogFilterDelay"/>.
		/// </summary>
		public const int MinUpdateLogFilterDelay = 300;


		/// <summary>
		/// Default font family of log.
		/// </summary>
		public static string DefaultLogFontFamily { get; } = Global.Run(() =>
		{
			var targetFontFamilies = Global.Run(() =>
			{
				if (Platform.IsMacOS)
					return new string[] { "Menlo", "Monaco", "Courier New" };
				return new string[] { "Consolas", "Courier New" };
			});
			var installedFontFamilyNames = new HashSet<string>(FontManager.Current.GetInstalledFontFamilyNames());
			foreach (var targetFontFamily in targetFontFamilies)
			{
				if (installedFontFamilyNames.Contains(targetFontFamily))
					return targetFontFamily;
			}
			return FontManager.Current.DefaultFontFamilyName;
		});
	}
}
