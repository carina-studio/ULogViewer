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
		/// Select language automatically.
		/// </summary>
		public static readonly SettingKey<bool> SelectLanguageAutomatically = new SettingKey<bool>(nameof(SelectLanguageAutomatically), true);


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
