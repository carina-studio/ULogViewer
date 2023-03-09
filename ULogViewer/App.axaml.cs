using System.Runtime;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// ULogViewer application.
	/// </summary>
	class App : AppSuite.AppSuiteApplication, IULogViewerApplication
	{
		// Source of change list.
		class ChangeListSource : DocumentSource
		{
			public ChangeListSource(App app) : base(app)
			{ }
			public override IList<ApplicationCulture> SupportedCultures => new ApplicationCulture[]
			{
				ApplicationCulture.EN_US,
				ApplicationCulture.ZH_CN,
				ApplicationCulture.ZH_TW,
			};
			public override Uri Uri => this.Culture switch
			{
				ApplicationCulture.ZH_CN => this.Application.CreateAvaloniaResourceUri("/ChangeList-zh-CN.md"),
				ApplicationCulture.ZH_TW => this.Application.CreateAvaloniaResourceUri("/ChangeList-zh-TW.md"),
				_ => this.Application.CreateAvaloniaResourceUri("/ChangeList.md"),
			};
		}


		// Info of main window.
		class MainWindowInfo
		{ }


		// Source of document of Privacy Policy.
		class PrivacyPolicySource : DocumentSource
		{
			public PrivacyPolicySource(App app) : base(app)
			{ 
				this.SetToCurrentCulture();
			}
			public override IList<ApplicationCulture> SupportedCultures => new ApplicationCulture[]
			{
				ApplicationCulture.EN_US,
				ApplicationCulture.ZH_TW,
			};
			public override Uri Uri => this.Culture switch
			{
				ApplicationCulture.ZH_TW => this.Application.CreateAvaloniaResourceUri("/Resources/PrivacyPolicy-zh-TW.md"),
				_ => this.Application.CreateAvaloniaResourceUri("/Resources/PrivacyPolicy.md"),
			};
		}


		// Source of document of User Agreement.
		class UserAgreementSource : DocumentSource
		{
			public UserAgreementSource(App app) : base(app)
			{ 
				this.SetToCurrentCulture();
			}
			public override IList<ApplicationCulture> SupportedCultures => new ApplicationCulture[]
			{
				ApplicationCulture.EN_US,
				ApplicationCulture.ZH_TW,
			};
			public override Uri Uri => this.Culture switch
			{
				ApplicationCulture.ZH_TW => this.Application.CreateAvaloniaResourceUri("/Resources/UserAgreement-zh-TW.md"),
				_ => this.Application.CreateAvaloniaResourceUri("/Resources/UserAgreement.md"),
			};
		}


		// External dependency of Xcode command-line tools settings.
		class XcodeCmdLineToolsSettingExtDependency : ExternalDependency
		{
			// Constructor.
			public XcodeCmdLineToolsSettingExtDependency(App app) : base(app, "XcodeCommandLineToolsSetting", ExternalDependencyType.Configuration, ExternalDependencyPriority.RequiredByFeatures)
			{ }

			/// <inheritdoc/>
			protected override async Task<bool> OnCheckAvailabilityAsync() => await Task.Run(() =>
			{
				try
				{
					using var process = Process.Start(new ProcessStartInfo()
					{
						Arguments = "simctl help",
						CreateNoWindow = true,
						FileName = "xcrun",
						UseShellExecute = false,
					});
					if (process != null)
					{
						process.WaitForExit();
						return process.ExitCode == 0;
					}
					return false;
				}
				catch
				{
					return false;
				}
			}, CancellationToken.None);
		}


		// Constants.
		const string InitialLogProfileKey = "InitialLogProfile";


		// Static fields.
		static readonly SettingKey<string> LegacyCultureSettingKey = new("Culture", "");
		static readonly SettingKey<string> LegacyThemeModeSettingKey = new("ThemeMode", "");
		static readonly SettingKey<bool> LegacySaveMemoryAggressivelySettingKey = new("SaveMemoryAggressively", false);


		// Fields.
		Controls.AppOptionsDialog? appOptionsDialog;
		IResourceProvider? compactResources;
		IDisposable? compactResourcesToken;
		ExternalDependency[] externalDependencies = Array.Empty<ExternalDependency>();
		readonly Dictionary<CarinaStudio.Controls.Window, MainWindowInfo> mainWindowInfoMap = new();
		readonly Stopwatch stopwatch = new();


		// Constructor.
		public App()
		{
			// setup name
			this.Name = "ULogViewer";

			// check Linux distribution
			if (Platform.IsLinux)
				this.Logger.LogDebug("Linux distribution: {linuxDistribution}", Platform.LinuxDistribution);
		}


		/// <inheritdoc/>
		protected override bool AllowMultipleMainWindows => true;


		/// <inheritdoc/>
		public override DocumentSource? ChangeList => new ChangeListSource(this);


		/// <inheritdoc/>
        public override AppSuite.ViewModels.ApplicationInfo CreateApplicationInfoViewModel() => 
			new ViewModels.AppInfo();


        /// <inheritdoc/>
        public override AppSuite.ViewModels.ApplicationOptions CreateApplicationOptionsViewModel() =>
			new ViewModels.AppOptions();


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
		public override int ExternalDependenciesVersion => 4;


		// Accept update for testing purpose.
        //protected override bool ForceAcceptingUpdateInfo => true;


        // Initialize.
        public override void Initialize() => AvaloniaXamlLoader.Load(this);


		// Support multi-instances.
		protected override bool IsMultipleProcessesSupported => false;


		/// <inheritdoc/>
		public bool IsTesting => false;


		// Program entry.
		[STAThread]
		static void Main(string[] args) => BuildApplication<App>()
			.StartWithClassicDesktopLifetime(args);


		// Create main window.
        protected override CarinaStudio.Controls.Window OnCreateMainWindow() => new MainWindow().Also(it =>
		{
			var info = new MainWindowInfo()
			{
				//
			};
			this.mainWindowInfoMap.Add(it, info);
		});


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
						this.Logger.LogWarning("Initial log profile is '{profileName}'", profile?.Name);
						return profile;
					}
					this.Logger.LogError("Cannot find initial log profile by ID '{id}'", it);
					return null;
				}) ?? this.Settings.GetValueOrDefault(SettingKeys.InitialLogProfile).Let(it =>
				{
					if (string.IsNullOrEmpty(it))
						return null;
					var profile = LogProfileManager.Default.GetProfileOrDefault(it);
					if (profile != null)
					{
						this.Logger.LogWarning("Initial log profile is '{profileName}'", profile?.Name);
						return profile;
					}
					this.Logger.LogError("Cannot find initial log profile by ID '{id}'", it);
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
				this.Logger.LogWarning("No string resources for {cultureInfo}", cultureInfo);
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
					this.Logger.LogWarning("No platform-specific string resources for {cultureInfo}", cultureInfo);
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
					this.Logger.LogWarning("No platform-specific string resources for {cultureInfo}", cultureInfo);
			}
			return resources;
		}


		// Load theme.
		protected override IStyle? OnLoadTheme(AppSuite.ThemeMode themeMode, bool useCompactUI)
		{
			// load resources
			if (!useCompactUI)
				this.compactResourcesToken = this.compactResourcesToken.DisposeAndReturnNull();
			else
			{
				this.compactResources ??= new ResourceInclude()
				{
					Source = new Uri("avares://ULogViewer/Styles/Resources-Compact.axaml")
				};
				this.compactResourcesToken ??= this.AddCustomResource(this.compactResources);
			}

			// load styles
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
			// detach from main window
			this.mainWindowInfoMap.Remove(mainWindow);

			// wait for I/O completion of log analysis rules
			await KeyLogAnalysisRuleSetManager.Default.WaitForIOTaskCompletion();
			await LogAnalysisScriptSetManager.Default.WaitForIOTaskCompletion();
			await OperationCountingAnalysisRuleSetManager.Default.WaitForIOTaskCompletion();
			await OperationDurationAnalysisRuleSetManager.Default.WaitForIOTaskCompletion();

			// wait for I/O completion of log text filters
			await PredefinedLogTextFilterManager.Default.WaitForIOTaskCompletion();

			// wait for I/O completion of log profiles
			await LogProfileManager.Default.WaitForIOTaskCompletion();

			// wait for I/O completion of log data source providers
			await LogDataSourceProviders.WaitForIOTaskCompletion();

			// call base
			await base.OnMainWindowClosedAsync(mainWindow, viewModel);
		}


		// Called when user click the native menu item.
		void OnNativeMenuItemClick(object? sender, EventArgs e)
		{
			switch ((sender as NativeMenuItem)?.CommandParameter as string)
			{
				case "AppInfo":
					this.ShowApplicationInfoDialog();
					break;
				case "AppOptions":
					this.ShowApplicationOptionsDialog();
					break;
				case "CheckForUpdate":
					this.CheckForApplicationUpdate();
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
			_ = this.ShowMainWindowAsync();
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


		/// <inheritdoc/>
		protected override AppSuite.Controls.SplashWindowParams OnPrepareSplashWindow() => base.OnPrepareSplashWindow().Also((ref AppSuite.Controls.SplashWindowParams param) =>
		{
			param.AccentColor = Avalonia.Media.Color.FromRgb(0x8a, 0x5c, 0xe6);
		});


		// Prepare starting.
		protected override async Task OnPrepareStartingAsync()
		{
			// setup log output rules
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
			if (!this.IsDebugMode)
				NLog.LogManager.Configuration.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, "methodCall");
			else
				NLog.LogManager.Configuration.AddRuleForAllLevels("methodCall");
			NLog.LogManager.ReconfigExistingLoggers();

			// start timer
			if (this.IsDebugMode)
				this.stopwatch.Start();

			// setup external dependencies
			this.externalDependencies = new List<ExternalDependency>().Also(it =>
			{
				it.Add(new ExecutableExternalDependency(this, "AndroidSDK", ExternalDependencyPriority.RequiredByFeatures, "adb", new Uri("https://developer.android.com/"), new Uri("https://developer.android.com/studio")));
				it.Add(new ExecutableExternalDependency(this, "Git", ExternalDependencyPriority.RequiredByFeatures, "git", new Uri("https://git-scm.com/"), new Uri("https://git-scm.com/downloads")));
				if (Platform.IsNotWindows)
					it.Add(new ExecutableExternalDependency(this, "TraceConv", ExternalDependencyPriority.RequiredByFeatures, "traceconv", new Uri("https://perfetto.dev/docs/quickstart/traceconv"), new Uri("https://perfetto.dev/docs/quickstart/traceconv#setup")));
				if (Platform.IsMacOS)
				{
					it.Add(new ExecutableExternalDependency(this, "LibIMobileDevice", ExternalDependencyPriority.RequiredByFeatures, "idevicesyslog", new Uri("https://libimobiledevice.org/"), new Uri("https://formulae.brew.sh/formula/libimobiledevice")));
					it.Add(new ExecutableExternalDependency(this, "XcodeCommandLineTools", ExternalDependencyPriority.RequiredByFeatures, "xcrun", new Uri("https://developer.apple.com/xcode/"), new Uri("https://developer.apple.com/download/all/?q=xcode")));
					it.Add(new XcodeCmdLineToolsSettingExtDependency(this));
				}
			}).ToArray();

			// call base
			await base.OnPrepareStartingAsync();
			this.UpdateSplashWindowProgress(0.1);

			// setup GC settings
			this.UpdateGCSettings();

			// setup internal test cases
			this.SetupTestCases();

			// initialize syntax highlighting service
			await SyntaxHighlighting.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.15);

			// initialize control fonts
			Controls.ControlFonts.Initialize(this);

			// initialize search providers
			Net.SearchProviderManager.Initialize(this);

			// find menu items
			if (Platform.IsMacOS)
			{
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
						}
					}
				});
			}

			// start initializing text shell manager
			var initTextShellManagerTask = TextShellManager.InitializeAsync(this);

			// initialize log data source providers
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializeLogProfiles"));
			await LogDataSourceProviders.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.25);

			// initialize predefined log text filters
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializePredefinedLogTextFilters"));
			await PredefinedLogTextFilterManager.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.5);

			// initialize log analysis rules
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializeLogAnalysisRules"));
			await KeyLogAnalysisRuleSetManager.InitializeAsync(this);
			await LogAnalysisScriptSetManager.InitializeAsync(this);
			await OperationCountingAnalysisRuleSetManager.InitializeAsync(this);
			await OperationDurationAnalysisRuleSetManager.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.6);

			// initialize log profiles
			await LogProfileManager.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.75);

			// complete initializing text shell manager
			await initTextShellManagerTask;

			// show main window
			if (!this.IsRestoringMainWindowsRequested)
				_ = this.ShowMainWindowAsync();
		}


		/// <inheritdoc/>
        protected override async Task<bool> OnRestoreMainWindowsAsync()
        {
            if (await base.OnRestoreMainWindowsAsync())
				return true;
			await this.ShowMainWindowAsync();
			return false;
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


		// Select whether to enter testing mode or not.
        protected override bool OnSelectEnteringTestingMode()
        {
#if DEBUG
			return true;
#else
			return base.OnSelectEnteringTestingMode();
#endif
        }


		/// <inheritdoc/>
		protected override void OnSettingChanged(SettingChangedEventArgs e)
		{
			base.OnSettingChanged(e);
			if (e.Key == SettingKeys.MemoryUsagePolicy)
				this.UpdateGCSettings();
		}


		/// <inheritdoc/>
        protected override bool OnTryExitingBackgroundMode()
        {
            if (base.OnTryExitingBackgroundMode())
				return true;
			if (this.MainWindows.IsEmpty())
				_ = this.ShowMainWindowAsync();
			return true;
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

			// upgrade memory usage policy
			if (oldVersion < 4)
			{
				if (settings.GetValueOrDefault(LegacySaveMemoryAggressivelySettingKey))
					settings.SetValue<MemoryUsagePolicy>(SettingKeys.MemoryUsagePolicy, MemoryUsagePolicy.LessMemoryUsage);
			}
		}


		// URI of package manifest.
		public override IEnumerable<Uri> PackageManifestUris => !this.Settings.GetValueOrDefault(AppSuite.SettingKeys.AcceptNonStableApplicationUpdate)
			? new[]{ Uris.AppPackageManifest }
			: this.ReleasingType == ApplicationReleasingType.Development
				? new[]
				{
					Uris.DevelopmentAppPackageManifest,
					Uris.PreviewAppPackageManifest,
					Uris.AppPackageManifest,
				} 
				: new[]
				{
					Uris.PreviewAppPackageManifest,
					Uris.AppPackageManifest,
				};


        /// <summary>
        /// Get private memory usage by application in bytes.
        /// </summary>
        public long PrivateMemoryUsage { get; private set; }


		/// <inheritdoc/>
		public override DocumentSource? PrivacyPolicy { get => new PrivacyPolicySource(this); }


		/// <inheritdoc/>
		public override Version? PrivacyPolicyVersion => new(1, 3);


        // Releasing type.
        public override AppSuite.ApplicationReleasingType ReleasingType => AppSuite.ApplicationReleasingType.Development;


		// Version of settings.
		protected override int SettingsVersion => 4;


		// Setup internal test cases.
		void SetupTestCases()
		{
			if (!this.IsTestingMode)
				return;
			AppSuite.Testing.TestManager.Default.Let(it =>
			{
				// Sessions
				it.AddTestCase(typeof(Testing.Sessions.SessionLeakageTest));
			});
		}


		/// <inheritdoc/>
        public override async Task ShowApplicationOptionsDialogAsync(Avalonia.Controls.Window? owner, string? section = null)
		{
			// wait for current dialog
			if (this.appOptionsDialog != null)
			{
				this.appOptionsDialog.ActivateAndBringToFront();
				await this.appOptionsDialog.WaitForClosingDialogAsync();
				return;
			}

			// show dialog
			owner?.ActivateAndBringToFront();
			this.appOptionsDialog = new Controls.AppOptionsDialog()
			{
				InitSectionName = section,
			};
			ApplicationOptionsDialogResult result;
			try
			{
				result = await (owner != null
					? this.appOptionsDialog.ShowDialog<ApplicationOptionsDialogResult>(owner)
					: this.appOptionsDialog.ShowDialog<ApplicationOptionsDialogResult>());
			}
			finally
			{
				this.appOptionsDialog = null;
			}
			switch (result)
			{
				case AppSuite.Controls.ApplicationOptionsDialogResult.RestartApplicationNeeded:
					this.Logger.LogWarning("Restart application");
					this.Restart(this.IsRunningAsAdministrator);
					break;
				case AppSuite.Controls.ApplicationOptionsDialogResult.RestartMainWindowsNeeded:
					this.Logger.LogWarning("Restart main windows");
					_ = this.RestartRootWindowsAsync();
					break;
			}
		}


		// Update GC settings.
		void UpdateGCSettings()
		{
			// latency mode
			var latencyMode = this.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy) switch
			{
				MemoryUsagePolicy.LessMemoryUsage => GCLatencyMode.Batch,
				MemoryUsagePolicy.BetterPerformance => GCLatencyMode.LowLatency,
				_ => GCLatencyMode.Interactive,
			};
			this.Logger.LogDebug("Set GC latency mode to {mode}", latencyMode);
			GCSettings.LatencyMode = latencyMode;
		}


		/// <inheritdoc/>
		public override DocumentSource? UserAgreement { get => new UserAgreementSource(this); }


		/// <inheritdoc/>
		public override Version? UserAgreementVersion => new(2, 0);
    }
}