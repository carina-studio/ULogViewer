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
		/// Select log files immediately when they are needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectLogFilesWhenNeeded = new SettingKey<bool>(nameof(SelectLogFilesWhenNeeded), false);
		/// <summary>
		/// Select language automatically.
		/// </summary>
		public static readonly SettingKey<bool> SelectLanguageAutomatically = new SettingKey<bool>(nameof(SelectLanguageAutomatically), true);
		/// <summary>
		/// Select working directory immediately when it is needed.
		/// </summary>
		public static readonly SettingKey<bool> SelectWorkingDirectoryWhenNeeded = new SettingKey<bool>(nameof(SelectWorkingDirectoryWhenNeeded), true);
		/// <summary>
		/// Use dark mode UI or not.
		/// </summary>
		public static readonly SettingKey<bool> DarkMode = new SettingKey<bool>(nameof(DarkMode), true);


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
