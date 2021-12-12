using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// ULogViewer application.
	/// </summary>
	class App : AppSuite.AppSuiteApplication, IULogViewerApplication
	{
		// Constants.
		const string InitialLogProfileKey = "InitialLogProfile";


		// Static fields.
		static readonly SettingKey<string> LegacyCultureSettingKey = new SettingKey<string>("Culture", "");
		static readonly SettingKey<string> LegacyThemeModeSettingKey = new SettingKey<string>("ThemeMode", "");


		// Constructor.
		public App()
		{
			// setup name
			this.Name = "ULogViewer";

			// check Linux distribution
			if (Platform.IsLinux)
				this.Logger.LogDebug($"Linux distribution: {Platform.LinuxDistribution}");
		}


		/// <summary>
		/// Get <see cref="App"/> instance for current process.
		/// </summary>
		public static new App Current
		{
			get => (App)Application.Current;
		}


        // Initialize.
        public override void Initialize() => AvaloniaXamlLoader.Load(this);


		// Support multi-instances.
		protected override bool IsMultipleProcessesSupported => true;


        /// <summary>
        /// Check whether using system accent color is supported on current platform or not.
        /// </summary>
        public bool IsSystemAccentColorSupported { get; } = Global.Run(() =>
		{
#if WINDOWS_ONLY
			/*
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return Environment.OSVersion.Version >= new Version(10, 0, 17763);
			*/
#endif
			return false;
		});


		/// <inheritdoc/>
		public bool IsTesting => false;


		// Program entry.
		[STAThread]
		static void Main(string[] args) => BuildApplication<App>().StartWithClassicDesktopLifetime(args);


		// Create main window.
        protected override CarinaStudio.Controls.Window OnCreateMainWindow() => new MainWindow();


		// Create view-model for main window.
		protected override ViewModel OnCreateMainWindowViewModel(JsonElement? savedState) => new Workspace(savedState).Also(it =>
		{
			var value = (object?)null;
			if (!savedState.HasValue)
			{
				var initialProfile = Global.Run(() =>
				{
					if (this.LaunchOptions.TryGetValue(InitialLogProfileKey, out value) && value is string strValue)
						return strValue;
					return null;
				})?.Let(it =>
				{
					if (LogProfiles.TryFindProfileById(it, out var profile))
					{
						this.Logger.LogWarning($"Initial log profile is '{profile?.Name}'");
						return profile;
					}
					this.Logger.LogError($"Cannot find initial log profile by ID '{it}'");
					return null;
				}) ?? this.Settings.GetValueOrDefault(SettingKeys.InitialLogProfile).Let(it =>
				{
					if (string.IsNullOrEmpty(it))
						return null;
					if (LogProfiles.TryFindProfileById(it, out var profile))
					{
						this.Logger.LogWarning($"Initial log profile is '{profile?.Name}'");
						return profile;
					}
					this.Logger.LogError($"Cannot find initial log profile by ID '{it}'");
					return null;
				});
				if (initialProfile != null)
					it.CreateSession(initialProfile);
			}
		});


		// Load default string resource.
        protected override IResourceProvider? OnLoadDefaultStringResource()
        {
			var resources = (IResourceProvider)new ResourceInclude()
			{
				Source = new Uri("avares://ULogViewer/Strings/Default.axaml")
			};
			if (Platform.IsLinux)
			{
				resources = new ResourceDictionary().Also(it =>
				{
					it.MergedDictionaries.Add(resources);
					it.MergedDictionaries.Add(new ResourceInclude()
					{
						Source = new Uri("avares://ULogViewer/Strings/Default-Linux.axaml")
					});
				});
			}
			else if (Platform.IsMacOS)
			{
				resources = new ResourceDictionary().Also(it =>
				{
					it.MergedDictionaries.Add(resources);
					it.MergedDictionaries.Add(new ResourceInclude()
					{
						Source = new Uri("avares://ULogViewer/Strings/Default-OSX.axaml")
					});
				});
			}
			return resources;
        }


		// Load strings for specific culture.
        protected override IResourceProvider? OnLoadStringResource(CultureInfo cultureInfo)
        {
			var resources = (IResourceProvider?)null;
			try
			{
				resources = new ResourceInclude().Also(it =>
				{
					it.Source = new Uri($"avares://ULogViewer/Strings/{cultureInfo}.axaml");
					_ = it.Loaded;
				});
			}
			catch
			{
				this.Logger.LogWarning($"No string resources for {cultureInfo}");
				return null;
			}
			try
			{
				if (Platform.IsLinux)
				{
					var platformResources = new ResourceInclude().Also(it =>
					{
						it.Source = new Uri($"avares://ULogViewer/Strings/{cultureInfo}-Linux.axaml");
						_ = it.Loaded;
					});
					resources = new ResourceDictionary().Also(it =>
					{
						it.MergedDictionaries.Add(resources);
						it.MergedDictionaries.Add(platformResources);
					});
				}
				else if (Platform.IsMacOS)
				{
					var platformResources = new ResourceInclude().Also(it =>
					{
						it.Source = new Uri($"avares://ULogViewer/Strings/{cultureInfo}-OSX.axaml");
						_ = it.Loaded;
					});
					resources = new ResourceDictionary().Also(it =>
					{
						it.MergedDictionaries.Add(resources);
						it.MergedDictionaries.Add(platformResources);
					});
				}
			}
			catch
			{
				this.Logger.LogWarning($"No platform-specific string resources for {cultureInfo}");
			}
			return resources;
		}


		// Load theme.
		protected override IStyle? OnLoadTheme(AppSuite.ThemeMode themeMode)
		{
			var uri = themeMode switch
			{
				AppSuite.ThemeMode.Light => new Uri($"avares://ULogViewer/Styles/Light.axaml"),
				_ => new Uri($"avares://ULogViewer/Styles/Dark.axaml"),
			};
			return new StyleInclude(new Uri("avares://ULogViewer/")).Also(it =>
			{
				it.Source = uri;
				_ = it.Loaded;
			});
		}


        // Called when main window closed.
        protected override async Task OnMainWindowClosedAsync(CarinaStudio.Controls.Window mainWindow, ViewModel viewModel)
        {
			// save predefined log text filters
			await PredefinedLogTextFilters.SaveAllAsync();

			// wait for IO completion of log profiles
			await LogProfiles.WaitForIOCompletionAsync();

			// call base
			await base.OnMainWindowClosedAsync(mainWindow, viewModel);
		}


		// Parse arguments.
        protected override int OnParseArguments(string[] args, int index, IDictionary<string, object> launchOptions)
        {
			switch (args[index])
			{
				case "-profile":
					if (index < args.Length - 1)
						launchOptions[InitialLogProfileKey] = args[++index];
					else
						this.Logger.LogError("ID of initial log profile is not specified");
					return ++index;
				default:
					return base.OnParseArguments(args, index, launchOptions);
			}
        }


		// Prepare starting.
		protected override async Task OnPrepareStartingAsync()
		{
			// setup log output rules
			if (this.IsDebugMode)
			{
				NLog.LogManager.Configuration.AddTarget(new NLog.Targets.MethodCallTarget("methodCall").Also(it =>
				{
					it.ClassName = "CarinaStudio.ULogViewer.MemoryLogger, ULogViewer";
					it.MethodName = "Log";
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${longdate}")));
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${processid}")));
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${threadid}")));
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${level:uppercase=true}")));
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${logger:shortName=true}")));
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${message}")));
					it.Parameters.Add(new NLog.Targets.MethodCallParameter(new NLog.Layouts.SimpleLayout("${exception:format=tostring}")));
				}));
				NLog.LogManager.Configuration.AddRuleForAllLevels("methodCall");
				NLog.LogManager.ReconfigExistingLoggers();
			}

			// call base
			await base.OnPrepareStartingAsync();

			// initialize log data source providers
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializeLogProfiles"));
			LogDataSourceProviders.Initialize(this);

			// initialize log profiles
			await LogProfiles.InitializeAsync(this);

			// initialize predefined log text filters
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializePredefinedLogTextFilters"));
			await PredefinedLogTextFilters.InitializeAsync(this);

			// show main window
			if (!this.IsRestoringMainWindowsRequested)
				this.ShowMainWindow();
		}


        // Select whether to enter debug mode or not.
        protected override bool OnSelectEnteringDebugMode()
        {
#if DEBUG
			return true;
#else
			return base.OnSelectEnteringDebugMode();
#endif
        }


        // Upgrade settings.
        protected override void OnUpgradeSettings(ISettings settings, int oldVersion, int newVersion)
        {
			// call base
            base.OnUpgradeSettings(settings, oldVersion, newVersion);

			// upgrade culture
			if (oldVersion < 2)
			{
				settings.GetValueOrDefault(LegacyCultureSettingKey).Let(oldValue =>
				{
					settings.ResetValue(LegacyCultureSettingKey);
					if (Enum.TryParse<AppSuite.ApplicationCulture>(oldValue, out var culture))
						settings.SetValue<AppSuite.ApplicationCulture>(AppSuite.SettingKeys.Culture, culture);
				});
			}

			// upgrade theme mode
			if (oldVersion < 2)
			{
				settings.GetValueOrDefault(LegacyThemeModeSettingKey).Let(oldValue =>
				{
					settings.ResetValue(LegacyThemeModeSettingKey);
					if (Enum.TryParse<AppSuite.ThemeMode>(oldValue, out var themeMode))
						settings.SetValue<AppSuite.ThemeMode>(AppSuite.SettingKeys.ThemeMode, themeMode);
				});
			}
		}


		// URI of package manifest.
		public override Uri? PackageManifestUri => this.Settings.GetValueOrDefault(AppSuite.SettingKeys.AcceptNonStableApplicationUpdate)
			? Uris.PreviewAppPackageManifest
			: Uris.AppPackageManifest;


        /// <summary>
        /// Get private memory usage by application in bytes.
        /// </summary>
        public long PrivateMemoryUsage { get; private set; }


		/// <inheritdoc/>
		public override Version? PrivacyPolicyVersion => new Version(1, 1);


        // Releasing type.
        public override AppSuite.ApplicationReleasingType ReleasingType => AppSuite.ApplicationReleasingType.Preview;


		// Version of settings.
		protected override int SettingsVersion => 2;


		/// <inheritdoc/>
		public override Version? UserAgreementVersion => new Version(1, 1);
    }
}