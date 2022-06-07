using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CarinaStudio.AppSuite;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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


		// Fields.
		ExternalDependency[] externalDependencies = new ExternalDependency[0];
		NativeMenuItem? toolsNativeMenuItem;


		// Constructor.
		public App()
		{
			// setup name
			this.Name = "ULogViewer";

			// check Linux distribution
			if (Platform.IsLinux)
				this.Logger.LogDebug($"Linux distribution: {Platform.LinuxDistribution}");
		}


		/// <inheritdoc/>
		protected override bool AllowMultipleMainWindows => true;



		/// <summary>
		/// Get <see cref="App"/> instance for current process.
		/// </summary>
		public static new App Current
		{
			get => (App)Application.Current;
		}


		/// <inheritdoc/>
		public override IEnumerable<ExternalDependency> ExternalDependencies { get => this.externalDependencies; }


		/// <inheritdoc/>
		public override int ExternalDependenciesVersion => 1;


		// Accept update for testing purpose.
        //protected override bool ForceAcceptingUpdateInfo => true;


        // Initialize.
        public override void Initialize() => AvaloniaXamlLoader.Load(this);


		// Support multi-instances.
		protected override bool IsMultipleProcessesSupported => false;


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
		static void Main(string[] args) => BuildApplication<App>()
			.StartWithClassicDesktopLifetime(args);


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
					var profile = LogProfileManager.Default.GetProfileOrDefault(it);
					if (profile != null)
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
					var profile = LogProfileManager.Default.GetProfileOrDefault(it);
					if (profile != null)
					{
						this.Logger.LogWarning($"Initial log profile is '{profile?.Name}'");
						return profile;
					}
					this.Logger.LogError($"Cannot find initial log profile by ID '{it}'");
					return null;
				});
				if (initialProfile != null)
					it.CreateAndAttachSession(initialProfile);
			}
		});


		// Load default string resource.
        protected override IResourceProvider? OnLoadDefaultStringResource()
        {
			var resources = this.LoadStringResource(new Uri("avares://ULogViewer/Strings/Default.axaml")).AsNonNull();
			if (Platform.IsLinux)
			{
				resources = new ResourceDictionary().Also(it =>
				{
					it.MergedDictionaries.Add(resources);
					it.MergedDictionaries.Add(this.LoadStringResource(new Uri("avares://ULogViewer/Strings/Default-Linux.axaml")).AsNonNull());
				});
			}
			else if (Platform.IsMacOS)
			{
				resources = new ResourceDictionary().Also(it =>
				{
					it.MergedDictionaries.Add(resources);
					it.MergedDictionaries.Add(this.LoadStringResource(new Uri("avares://ULogViewer/Strings/Default-OSX.axaml")).AsNonNull());
				});
			}
			return resources;
        }


		// Load strings for specific culture.
        protected override IResourceProvider? OnLoadStringResource(CultureInfo cultureInfo)
        {
			var resources = this.LoadStringResource(new Uri($"avares://ULogViewer/Strings/{cultureInfo}.axaml"));
			if (resources == null)
			{
				this.Logger.LogWarning($"No string resources for {cultureInfo}");
				return null;
			}
			if (Platform.IsLinux)
			{
				var platformResources = this.LoadStringResource(new Uri($"avares://ULogViewer/Strings/{cultureInfo}-Linux.axaml"));
				if (platformResources != null)
				{
					resources = new ResourceDictionary().Also(it =>
					{
						it.MergedDictionaries.Add(resources);
						it.MergedDictionaries.Add(platformResources);
					});
				}
				else
					this.Logger.LogWarning($"No platform-specific string resources for {cultureInfo}");
			}
			else if (Platform.IsMacOS)
			{
				var platformResources = this.LoadStringResource(new Uri($"avares://ULogViewer/Strings/{cultureInfo}-OSX.axaml"));
				if (platformResources != null)
				{
					resources = new ResourceDictionary().Also(it =>
					{
						it.MergedDictionaries.Add(resources);
						it.MergedDictionaries.Add(platformResources);
					});
				}
				else
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
			// wait for I/O completion of log analysis rules
			await KeyLogAnalysisRuleSetManager.Default.WaitForIOTaskCompletion();
			await OperationDurationAnalysisRuleSetManager.Default.WaitForIOTaskCompletion();

			// wait for I/O completion of log text filters
			await PredefinedLogTextFilterManager.Default.WaitForIOTaskCompletion();

			// wait for I/O completion of log profiles
			await LogProfileManager.Default.WaitForIOTaskCompletion();

			// call base
			await base.OnMainWindowClosedAsync(mainWindow, viewModel);
		}


		// Called when user click the native menu item.
		void OnNativeMenuItemClick(object? sender, EventArgs e)
		{
			switch ((sender as NativeMenuItem)?.CommandParameter as string)
			{
				case "AppInfo":
					(this.LatestActiveMainWindow as MainWindow)?.ShowAppInfo();
					break;
				case "AppOptions":
					(this.LatestActiveMainWindow as MainWindow)?.ShowAppOptions();
					break;
				case "CheckForUpdate":
					(this.LatestActiveMainWindow as MainWindow)?.CheckForAppUpdate();
					break;
				case "EditConfiguration":
					(this.LatestActiveMainWindow as MainWindow)?.EditConfiguration();
					break;
				case "EditPersistentState":
					(this.LatestActiveMainWindow as MainWindow)?.EditPersistentState();
					break;
				case "Shutdown":
					this.Shutdown();
					break;
			}
		}


		/// <inheritdoc/>
        protected override void OnNewInstanceLaunched(IDictionary<string, object> launchOptions)
        {
			// call base
            base.OnNewInstanceLaunched(launchOptions);

			// show main window
			this.ShowMainWindow();
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

			// prepare platform specific resources
			if (Platform.IsMacOS)
			{
				this.Resources.MergedDictionaries.Add(new ResourceInclude()
				{
					Source = new Uri("avares://ULogViewer/Styles/Resources-OSX.axaml")
				});
			}

			// setup external dependencies
			this.externalDependencies = new ExternalDependency[] {
				new ExecutableExternalDependency(this, "AndroidSDK", ExternalDependencyPriority.RequiredByFeatures, "adb", new Uri("https://developer.android.com/"), new Uri("https://developer.android.com/studio")),
				new ExecutableExternalDependency(this, "Git", ExternalDependencyPriority.RequiredByFeatures, "git", new Uri("https://git-scm.com/"), new Uri("https://git-scm.com/downloads")),
			};

			// call base
			await base.OnPrepareStartingAsync();

			// find menu items
			NativeMenu.GetMenu(this)?.Let(menu =>
			{
				for (var i = menu.Items.Count - 1; i >= 0 ; --i)
				{
					var item = menu.Items[i];
					if (item is not NativeMenuItem menuItem)
						continue;
					switch (menuItem.CommandParameter as string)
					{
						case "EditConfiguration":
							if (!this.IsDebugMode)
							{
								menu.Items.RemoveAt(i--);
								menu.Items.RemoveAt(i); // Separator
							}
							break;
						case "EditPersistentState":
							if (!this.IsDebugMode)
								menu.Items.RemoveAt(i);
							break;
						case "Tools":
							toolsNativeMenuItem = menuItem;
							menu.Items.RemoveAt(i--);
							menu.Items.RemoveAt(i); // Separator
							break;
					}
				}
			});

			// initialize log data source providers
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializeLogProfiles"));
			LogDataSourceProviders.Initialize(this);

			// initialize log profiles
			await LogProfileManager.InitializeAsync(this);

			// initialize predefined log text filters
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializePredefinedLogTextFilters"));
			await PredefinedLogTextFilterManager.InitializeAsync(this);

			// initialize log analysis rules
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializeLogAnalysisRules"));
			await KeyLogAnalysisRuleSetManager.InitializeAsync(this);
			await OperationDurationAnalysisRuleSetManager.InitializeAsync(this);

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
			if (oldVersion < 3)
			{
				if (Platform.IsMacOS && settings.GetValueOrDefault(AppSuite.SettingKeys.ThemeMode) == AppSuite.ThemeMode.Light)
					settings.SetValue<AppSuite.ThemeMode>(AppSuite.SettingKeys.ThemeMode, AppSuite.ThemeMode.System);
			}
			else if (oldVersion < 2)
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
		public override Version? PrivacyPolicyVersion => new Version(1, 2);


        // Releasing type.
        public override AppSuite.ApplicationReleasingType ReleasingType => AppSuite.ApplicationReleasingType.Preview;


		// Version of settings.
		protected override int SettingsVersion => 3;


		/// <inheritdoc/>
		public override Version? UserAgreementVersion => new Version(1, 3);
    }
}