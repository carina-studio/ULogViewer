using System.Runtime;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Controls;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// ULogViewer application.
	/// </summary>
	class App : AppSuiteApplication, IULogViewerApplication
	{
		// Source of change list.
		class ChangeListSource : DocumentSource
		{
			public ChangeListSource(App app) : base(app)
			{ }
			public override IList<ApplicationCulture> SupportedCultures => new[]
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
			public override IList<ApplicationCulture> SupportedCultures => new[]
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
			public override IList<ApplicationCulture> SupportedCultures => new[]
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
		static readonly SettingKey<bool> LegacyShowLogPropertySeparators = new("ShowLogPropertySeparators", false);


		// Fields.
		AppOptionsDialog? appOptionsDialog;
		IResourceProvider? compactResources;
		IDisposable? compactResourcesToken;
		ExternalDependency[] externalDependencies = Array.Empty<ExternalDependency>();
		readonly Dictionary<CarinaStudio.Controls.Window, MainWindowInfo> mainWindowInfoMap = new();
		DocumentViewerWindow? quickStartGuideWindow;
		readonly Stopwatch stopwatch = new();


		// Constructor.
		public App()
		{
			LogToConsole("Initialize App instance");
			
			// setup name
			this.Name = "ULogViewer";

			// check Linux distribution
			if (Platform.IsLinux)
				this.Logger.LogDebug("Linux distribution: {linuxDistribution}", Platform.LinuxDistribution);
		}


		/// <inheritdoc/>
		protected override bool AllowMultipleMainWindows => true;


		/// <inheritdoc/>
		public override DocumentSource ChangeList => new ChangeListSource(this);


		/// <inheritdoc/>
        public override AppSuite.ViewModels.ApplicationInfo CreateApplicationInfoViewModel() => 
			new AppInfo();


        /// <inheritdoc/>
        public override AppSuite.ViewModels.ApplicationOptions CreateApplicationOptionsViewModel() =>
			new AppOptions();


		/// <summary>
		/// Get <see cref="App"/> instance for current process.
		/// </summary>
		public static new App Current => (App)Application.Current;


		/// <inheritdoc/>
		public override IEnumerable<ExternalDependency> ExternalDependencies => this.externalDependencies;


		/// <inheritdoc/>
		public override int ExternalDependenciesVersion => 4;


		// Accept update for testing purpose.
        //protected override bool ForceAcceptingUpdateInfo => true;


        // Initialize.
        public override void Initialize() => AvaloniaXamlLoader.Load(this);


		// Support multi-instances.
		protected override bool IsMultipleProcessesSupported => false;


		// Program entry.
		[STAThread]
		static void Main(string[] args) => 
			BuildApplicationAndStart<App>(args);


		// Create main window.
        protected override AppSuite.Controls.MainWindow OnCreateMainWindow() => new MainWindow().Also(it =>
        {
	        var info = new MainWindowInfo();
			this.mainWindowInfoMap.Add(it, info);
		});


		// Create view-model for main window.
		protected override ViewModel OnCreateMainWindowViewModel(JsonElement? savedState) => new Workspace(savedState).Also(it =>
		{
			if (!savedState.HasValue && this.MainWindows.IsEmpty())
			{
				var initialProfile = Global.Run(() =>
				{
					if (this.LaunchOptions.TryGetValue(InitialLogProfileKey, out var value) && value is string strValue)
						return strValue;
					return null;
				})?.Let(it =>
				{
					var profile = LogProfileManager.Default.GetProfileOrDefault(it);
					if (profile != null)
					{
						this.Logger.LogWarning("Initial log profile is '{profileName}'", profile.Name);
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
						this.Logger.LogWarning("Initial log profile is '{profileName}'", profile.Name);
						return profile;
					}
					this.Logger.LogError("Cannot find initial log profile by ID '{id}'", it);
					return null;
				});
				if (initialProfile != null)
					it.CreateAndAttachSession(initialProfile);
			}
		});


		/// <inheritdoc/>
		protected override bool OnExceptionOccurredInApplicationLifetime(Exception ex)
		{
			switch (ex)
			{
				case IndexOutOfRangeException: // [Workaround] Prevent unexpected error occurred in TextBox
				{
					var stackTrace = ex.StackTrace ?? "";
					if (stackTrace.Contains("at Avalonia.Media.GlyphRun.FindNearestCharacterHit("))
					{
						this.Logger.LogWarning("Ignore IndexOutOfRangeException thrown by GlyphRun.FindNearestCharacterHit() caused by unknown reason");
						return true;
					}
					if (stackTrace.Contains(" at Avalonia.Media.GlyphRun.GetDistanceFromCharacterHit("))
					{
						this.Logger.LogWarning("Ignore IndexOutOfRangeException thrown by GlyphRun.GetDistanceFromCharacterHit() caused by unknown reason");
						return true;
					}
					break;
				}
				case InvalidOperationException: // [Workaround] Prevent unexpected error occurred in TextBlock
				{
					var stackTrace = ex.StackTrace ?? "";
					if (stackTrace.Contains("at Avalonia.Media.Typeface.get_GlyphTypeface("))
					{
						this.Logger.LogWarning("Ignore InvalidOperationException thrown by Typeface.GlyphTypeface caused by unknown reason");
						return true;
					}
					break;
				}
			}
			return base.OnExceptionOccurredInApplicationLifetime(ex);
		}


		// Load default string resource.
        protected override IResourceProvider OnLoadDefaultStringResource()
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
		protected override IStyle OnLoadTheme(ThemeMode themeMode, bool useCompactUI)
		{
			// load resources
			if (!useCompactUI)
				this.compactResourcesToken = this.compactResourcesToken.DisposeAndReturnNull();
			else
			{
				this.compactResources ??= new ResourceInclude(new Uri("avares://ULogViewer/"))
				{
					Source = new Uri("/Styles/Resources-Compact.axaml", UriKind.Relative)
				};
				this.compactResourcesToken ??= this.AddCustomResource(this.compactResources);
			}

			// load styles
			var uri = themeMode switch
			{
				ThemeMode.Light => new Uri("/Styles/Light.axaml", UriKind.Relative),
				_ => new Uri("/Styles/Dark.axaml", UriKind.Relative),
			};
			return new StyleInclude(new Uri("avares://ULogViewer/")).Also(it =>
			{
				it.Source = uri;
				_ = it.Loaded;
			});
		}


        // Called when main window closed.
        protected override async Task OnMainWindowClosedAsync(AppSuite.Controls.MainWindow mainWindow, ViewModel viewModel)
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
		// ReSharper disable once UnusedParameter.Local
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
				case "Feedback":
					this.OpenFeedbackPage();
					break;
				case "QuickStartGuide":
					this.ShowQuickStartGuideWindow();
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
        protected override async Task OnPrepareShuttingDownAsync(bool isCritical)
        {
	        if (isCritical)
		        LogTextFilterPhrasesDatabase.CloseAsync().Wait();
	        else
				await LogTextFilterPhrasesDatabase.CloseAsync();
	        await base.OnPrepareShuttingDownAsync(isCritical);
        }


        /// <inheritdoc/>
		protected override SplashWindowParams OnPrepareSplashWindow() => base.OnPrepareSplashWindow().Also((ref SplashWindowParams param) =>
		{
			param.AccentColor = Avalonia.Media.Color.FromRgb(0x8a, 0x5c, 0xe6);
			param.BackgroundImageOpacity = 0.8;
		});


		// Prepare starting.
		protected override async Task OnPrepareStartingAsync()
		{
			LogToConsole("Prepare starting (App)");
			
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
				// ReSharper disable StringLiteralTypo
				it.Add(new ExecutableExternalDependency(this, "AndroidSdkPlatformTools", ExternalDependencyPriority.RequiredByFeatures, "adb", new Uri("https://developer.android.com/tools/releases/platform-tools"), new Uri("https://developer.android.com/tools/releases/platform-tools#downloads")));
				it.Add(new ExecutableExternalDependency(this, "Git", ExternalDependencyPriority.RequiredByFeatures, "git", new Uri("https://git-scm.com/"), new Uri("https://git-scm.com/downloads")));
				it.Add(new ExecutableExternalDependency(this, "LibIMobileDevice", ExternalDependencyPriority.RequiredByFeatures, "idevicesyslog", new Uri("https://libimobiledevice.org/"), Global.Run(() =>
				{
					if (Platform.IsWindows)
						return new Uri("https://github.com/iFred09/libimobiledevice-windows");
					if (Platform.IsMacOS)
						return new Uri("https://formulae.brew.sh/formula/libimobiledevice");
					if (Platform.IsLinux)
						return new Uri("https://command-not-found.com/idevicesyslog");
					return null;
				})));
				if (Platform.IsNotWindows)
					it.Add(new ExecutableExternalDependency(this, "TraceConv", ExternalDependencyPriority.RequiredByFeatures, "traceconv", new Uri("https://perfetto.dev/docs/quickstart/traceconv"), new Uri("https://perfetto.dev/docs/quickstart/traceconv#setup")));
				if (Platform.IsMacOS)
				{
					it.Add(new ExecutableExternalDependency(this, "XcodeCommandLineTools", ExternalDependencyPriority.RequiredByFeatures, "xcrun", new Uri("https://developer.apple.com/xcode/"), new Uri("https://developer.apple.com/download/all/?q=xcode")));
					it.Add(new XcodeCmdLineToolsSettingExtDependency(this));
				}
				// ReSharper restore StringLiteralTypo
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
			ControlFonts.Initialize(this);

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
			this.UpdateSplashWindowProgress(0.4);

			// initialize predefined log text filters
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializePredefinedLogTextFilters"));
			await PredefinedLogTextFilterManager.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.5);
			
			// initialize log text filter phrases database
			await LogTextFilterPhrasesDatabase.InitializeAsync(this);
			await this.WaitForSplashWindowAnimationAsync();
			this.UpdateSplashWindowProgress(0.6);

			// initialize log analysis rules
			this.UpdateSplashWindowMessage(this.GetStringNonNull("SplashWindow.InitializeLogAnalysisRules"));
			await KeyLogAnalysisRuleSetManager.InitializeAsync(this);
			await LogAnalysisScriptSetManager.InitializeAsync(this);
			await OperationCountingAnalysisRuleSetManager.InitializeAsync(this);
			await OperationDurationAnalysisRuleSetManager.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.7);

			// initialize log profiles
			await LogProfileManager.InitializeAsync(this);
			this.UpdateSplashWindowProgress(0.85);

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
#if DEBUG || TESTING_MODE_BUILD
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
					if (Enum.TryParse<ApplicationCulture>(oldValue, out var culture))
						settings.SetValue<ApplicationCulture>(AppSuite.SettingKeys.Culture, culture);
				});
			}

			// upgrade theme mode
			if (oldVersion < 2)
			{
				settings.GetValueOrDefault(LegacyThemeModeSettingKey).Let(oldValue =>
				{
					settings.ResetValue(LegacyThemeModeSettingKey);
					if (Enum.TryParse<ThemeMode>(oldValue, out var themeMode))
						settings.SetValue<ThemeMode>(AppSuite.SettingKeys.ThemeMode, themeMode);
				});
			}
			if (oldVersion < 3)
			{
				if (Platform.IsMacOS && settings.GetValueOrDefault(AppSuite.SettingKeys.ThemeMode) == ThemeMode.Light)
					settings.SetValue<ThemeMode>(AppSuite.SettingKeys.ThemeMode, ThemeMode.System);
			}

			// upgrade memory usage policy
			if (oldVersion < 4)
			{
				if (settings.GetValueOrDefault(LegacySaveMemoryAggressivelySettingKey))
					settings.SetValue<MemoryUsagePolicy>(SettingKeys.MemoryUsagePolicy, MemoryUsagePolicy.LessMemoryUsage);
			}
			
			// determine whether tutorials are needed to be shown or not
			if (oldVersion == 4)
				this.PersistentState.SetValue<bool>(PredefinedLogTextFilterEditorDialog.IsGroupNameTutorialShownKey, false);
			
			// log property separators
			if (oldVersion == 5)
			{
				if (settings.GetValueOrDefault(LegacyShowLogPropertySeparators))
					settings.SetValue<LogSeparatorType>(SettingKeys.LogSeparators, LogSeparatorType.Vertical);
			}
        }
        
        
        /// <summary>
        /// Open feedback page.
        /// </summary>
        public void OpenFeedbackPage() =>
	        Platform.OpenLink("https://github.com/carina-studio/ULogViewer/issues");


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


		/// <inheritdoc/>
		public override DocumentSource PrivacyPolicy => new PrivacyPolicySource(this);


		/// <inheritdoc/>
		public override Version PrivacyPolicyVersion => new(1, 3);


        // Releasing type.
        public override ApplicationReleasingType ReleasingType => ApplicationReleasingType.Preview;


		// Version of settings.
		protected override int SettingsVersion => 6;


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
			this.appOptionsDialog = new AppOptionsDialog()
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
				case ApplicationOptionsDialogResult.RestartApplicationNeeded:
					this.Logger.LogWarning("Restart application");
					this.Restart(this.IsRunningAsAdministrator);
					break;
				case ApplicationOptionsDialogResult.RestartMainWindowsNeeded:
					this.Logger.LogWarning("Restart main windows");
					_ = this.RestartRootWindowsAsync();
					break;
			}
		}


		/// <summary>
		/// Show the window for quick-start guide.
		/// </summary>
		public void ShowQuickStartGuideWindow()
		{
			this.VerifyAccess();
			if (this.IsShutdownStarted)
				return;
			if (this.quickStartGuideWindow is not null)
				this.quickStartGuideWindow.ActivateAndBringToFront();
			else
			{
				this.quickStartGuideWindow = new DocumentViewerWindow().Also(it =>
				{
					it.Closed += (_, _) => this.quickStartGuideWindow = null;
					it.DocumentSource = new QuickStartGuideDocumentSource(this);
					it.Topmost = true;
				});
				this.quickStartGuideWindow.Show();
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
		public override DocumentSource UserAgreement => new UserAgreementSource(this);


		/// <inheritdoc/>
		public override Version UserAgreementVersion => new(2, 4);
    }
}