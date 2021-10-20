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
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
		const string RestoreStateRequestedKey = "RestoreStateRequested";


		// Static fields.
		static readonly SettingKey<string> LegacyCultureSettingKey = new SettingKey<string>("Culture", "");
		static readonly SettingKey<string> LegacyThemeModeSettingKey = new SettingKey<string>("ThemeMode", "");


		// Fields.
		bool isRestartRequested;
		bool isRestartAsAdminRequested;
		string? restartArgs;


		// Constructor.
		public App()
		{
			// setup name
			this.Name = "ULogViewer";

			// check whether process is running as admin or not
			if (Platform.IsWindows)
			{
#pragma warning disable CA1416
				using var identity = WindowsIdentity.GetCurrent();
				var principal = new WindowsPrincipal(identity);
				this.IsRunningAsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
			}
			if (this.IsRunningAsAdministrator)
				this.Logger.LogWarning("Application is running as administrator/superuser");

			// check Linux distribution
			this.Logger.LogDebug($"Linux distribution: {Platform.LinuxDistribution}");
		}


		// Avalonia configuration, don't remove; also used by visual designer.
		static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace().Also(it =>
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					it.With(new X11PlatformOptions());
			});


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


		// Program entry.
		[STAThread]
		static void Main(string[] args)
		{
			// start application
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

			// restart
			var app = App.Current;
			if (app != null && app.isRestartRequested)
			{
				try
				{
					if (app.isRestartAsAdminRequested)
						app.Logger.LogWarning("Restart as administrator/superuser");
					else
						app.Logger.LogWarning("Restart");
					var process = new Process().Also(process =>
					{
						process.StartInfo.Let(it =>
						{
							it.Arguments = app.restartArgs ?? "";
							it.FileName = (Process.GetCurrentProcess().MainModule?.FileName).AsNonNull();
							if (app.isRestartAsAdminRequested && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
							{
								it.UseShellExecute = true;
								it.Verb = "runas";
							}
						});
					});
					process.Start();
				}
				catch (Exception ex)
				{
					app.Logger.LogError(ex, "Unable to restart");
				}
			}
		}


		// Create log provider.
        protected override ILoggerProvider OnCreateLoggerProvider()
        {
			NLog.LogManager.Configuration = this.OpenManifestResourceStream("CarinaStudio.ULogViewer.NLog.config").Use(stream =>
			{
				using var xmlReader = XmlReader.Create(stream);
				return new NLog.Config.XmlLoggingConfiguration(xmlReader);
			});
#if DEBUG
			NLog.LogManager.Configuration.AddRuleForAllLevels("methodCall");
#endif
			return base.OnCreateLoggerProvider();
        }


		// Create main window.
        protected override CarinaStudio.Controls.Window OnCreateMainWindow(object? param) => new MainWindow();


		// Create view-model for main window.
		protected override ViewModel OnCreateMainWindowViewModel(object? param) => new Workspace().Also(it =>
		{
			var value = (object?)null;
			var isWorkspaceStateRestored = Global.Run(() =>
			{
				if (this.LaunchOptions.TryGetValue(RestoreStateRequestedKey, out value) && value is bool boolValue && boolValue)
				{
					it.RestoreState();
					return true;
				}
				return false;
			});
			if (!isWorkspaceStateRestored)
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
				it.ClearSavedState();
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
			// save instance state
			(viewModel as Workspace)?.SaveState();

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
				case "-restore-state":
					launchOptions[RestoreStateRequestedKey] = true;
					return ++index;
				default:
					return base.OnParseArguments(args, index, launchOptions);
			}
        }


		// Prepare starting.
        protected override async Task OnPrepareStartingAsync()
        {
			// output less logs
			if (!this.IsDebugMode)
			{
				NLog.LogManager.Configuration.RemoveRuleByName("logAllToFile");
				NLog.LogManager.Configuration.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, "file");
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


		// Releasing type.
		public override AppSuite.ApplicationReleasingType ReleasingType => AppSuite.ApplicationReleasingType.Preview;


		// Restart application.
		public bool Restart(string? args, bool asAdministrator)
		{
			// check state
			this.VerifyAccess();
			if (this.isRestartRequested)
			{
				if (!string.IsNullOrEmpty(args) && !string.IsNullOrEmpty(this.restartArgs) && args != this.restartArgs)
				{
					this.Logger.LogError("Try restarting application with different arguments");
					return false;
				}
				this.isRestartAsAdminRequested |= asAdministrator;
				this.restartArgs = args;
				if (this.isRestartAsAdminRequested)
					this.Logger.LogWarning("Already restarting as administrator/superuser");
				else
					this.Logger.LogWarning("Already restarting");
				return true;
			}

			// update state
			this.isRestartRequested = true;
			this.isRestartAsAdminRequested = asAdministrator;
			this.restartArgs = args;
			if (asAdministrator)
				this.Logger.LogWarning("Request restarting as administrator/superuser");
			else
				this.Logger.LogWarning("Request restarting");

			// shutdown
			this.Shutdown();
			return true;
		}


		// Version of settings.
		protected override int SettingsVersion => 2;


		// Interface implementations.
		public bool IsRunningAsAdministrator { get; private set; }
		public bool IsTesting => false;
	}
}