using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using CarinaStudio.AutoUpdate;
using CarinaStudio.AutoUpdate.Resolvers;
using CarinaStudio.Configuration;
using CarinaStudio.IO;
using CarinaStudio.Net;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NLog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.UI.ViewManagement;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application.
	/// </summary>
	class App : Application, IApplication
	{
		// Constants.
		const int ProcessInfoUpdateInterval = 1000;
		const string TextBoxFontFamilyResourceKey = "FontFamily.TextBox";
		const int UpdateCheckingInterval = 3600000; // 1 hr


		// Fields.
		ScheduledAction? checkUpdateInfoAction;
		bool isRestartRequested;
		bool isRestartAsAdminRequested;
		readonly ILogger logger;
		MainWindow? mainWindow;
		volatile Settings? persistentState;
		readonly string persistentStateFilePath;
		long previousProcessInfoUpdateTime;
		TimeSpan previousTotalProcessorTime;
		readonly Process process = Process.GetCurrentProcess();
		PropertyChangedEventHandler? propertyChangedHandlers;
		string? restartArgs;
		volatile Settings? settings;
		readonly TaskFactory settingsFileAccessTaskFactory = new TaskFactory(new FixedThreadsTaskScheduler(1));
		readonly string settingsFilePath;
		readonly Stopwatch stopWatch = new Stopwatch().Also(it => it.Start());
		SplashWindow? splashWindow;
		ResourceInclude? stringResources;
		CultureInfo? stringResourcesCulture;
		ResourceInclude? stringResourcesForOS;
		StyleInclude? styles;
		ThemeMode? stylesThemeMode;
		volatile SynchronizationContext? synchronizationContext;
		ResourceDictionary? systemAccentColorResources;
		AppUpdateInfo? updateInfo;
		ScheduledAction? updateProcessInfoAction;
		UISettings? windowsUISettings;
		Workspace? workspace;


		// Constructor.
		public App()
		{
			// setup logger
			NLog.LogManager.Configuration = this.OpenManifestResourceStream("CarinaStudio.ULogViewer.NLog.config").Use(stream =>
			{
				using var xmlReader = XmlReader.Create(stream);
				return new NLog.Config.XmlLoggingConfiguration(xmlReader);
			});
#if DEBUG
			NLog.LogManager.Configuration.AddRuleForAllLevels("methodCall");
#endif
			this.logger = this.LoggerFactory.CreateLogger("App");
			this.logger.LogWarning("App created");

			// setup global exception handler
			AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			{
				var exceptionObj = e.ExceptionObject;
				if (exceptionObj is Exception exception)
					this.logger.LogError(exception, "***** Unhandled application exception *****");
				else
					this.logger.LogError($"***** Unhandled application exception ***** {exceptionObj}");
			};

			// select private directory
			this.RootPrivateDirectoryPath = Path.GetDirectoryName(this.process.MainModule?.FileName) ?? throw new ArgumentException("Unable to get directory of application.");

			// prepare file path of settings
			this.persistentStateFilePath = Path.Combine(this.RootPrivateDirectoryPath, "PersistentState.json");
			this.settingsFilePath = Path.Combine(this.RootPrivateDirectoryPath, "Settings.json");

			// check whether process is running as admin or not
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				using var identity = WindowsIdentity.GetCurrent();
				var principal = new WindowsPrincipal(identity);
				this.IsRunningAsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
			}
			if (this.IsRunningAsAdministrator)
				this.logger.LogWarning("Application is running as administrator/superuser");
			
			// check Linux distribution
			this.CheckLinuxDistribution();
		}


		// Avalonia configuration, don't remove; also used by visual designer.
		static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace().Also(it =>
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					it.With(new X11PlatformOptions());
			});
		

		// Check Linux distribution.
		void CheckLinuxDistribution()
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return;
			try
			{
				using var reader = new StreamReader("/proc/version", Encoding.UTF8);
				this.LinuxDistribution = reader.ReadLine()?.Let(data =>
				{
					if (data.Contains("(Debian"))
						return LinuxDistribution.Debian;
					if (data.Contains("(Fedora"))
						return LinuxDistribution.Fedora;
					if (data.Contains("(Ubuntu"))
						return LinuxDistribution.Ubuntu;
					return LinuxDistribution.Unknown;
				}) ?? LinuxDistribution.Unknown;
				this.logger.LogDebug($"Linux distribution: {this.LinuxDistribution}");
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Failed to check Linux distribution");
			}
		}


		/// <summary>
		/// Check application update asynchronously.
		/// </summary>
		public async Task CheckUpdateInfoAsync()
		{
			// schedule next checking
			this.checkUpdateInfoAction?.Reschedule(UpdateCheckingInterval);

			// check update by package manifest
			var stopWatch = new Stopwatch().Also(it => it.Start());
			var packageResolver = new JsonPackageResolver() { Source = new WebRequestStreamProvider(Uris.AppPackageManifest) };
			this.logger.LogInformation("Start checking update");
			try
			{
				await packageResolver.StartAndWaitAsync();
			}
			catch(Exception ex)
			{
				this.logger.LogError(ex, "Failed to check update");
				return;
			}

			// delay to make UX better
			var delay = (1000 - stopWatch.ElapsedMilliseconds);
			if (delay > 0)
				await Task.Delay((int)delay);

			// check version
			var packageVersion = packageResolver.PackageVersion;
			if (packageVersion == null)
			{
				this.logger.LogError("No application version gotten from package manifest");
				return;
			}
			if (packageVersion <= this.Assembly.GetName().Version)
			{
				this.logger.LogInformation("This is the latest application");
				if (this.updateInfo != null)
				{
					this.updateInfo = null;
					this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateInfo)));
				}
				return;
			}

			// create update info
			this.logger.LogDebug($"New application version found: {packageVersion}");
			var updateInfo = new AppUpdateInfo(packageVersion, packageResolver.PageUri, packageResolver.PackageUri);
			if (updateInfo != this.updateInfo)
			{
				this.updateInfo = updateInfo;
				this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateInfo)));
			}
		}


		/// <summary>
		/// Get CPU usage of application in percentage.
		/// </summary>
		public double CpuUsagePercentage { get; private set; }


		/// <summary>
		/// Get <see cref="App"/> instance for current process.
		/// </summary>
		public static new App Current
		{
			get => (App)Application.Current;
		}


		// Deinitialize.
		void Deinitialize()
		{
			// dispose workspace
			this.workspace?.Dispose();

			// detach from system events
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				SystemEvents.UserPreferenceChanged -= this.OnWindowsUserPreferenceChanged;

			// cancel update checking
			this.checkUpdateInfoAction?.Cancel();

			this.logger.LogWarning("Stop");
		}


		// Transform RGB color values.
		static Color GammaTransform(Color color, double gamma)
		{
			double r = (color.R / 255.0);
			double g = (color.G / 255.0);
			double b = (color.B / 255.0);
			return Color.FromArgb(color.A, (byte)(Math.Pow(r, gamma) * 255 + 0.5), (byte)(Math.Pow(g, gamma) * 255 + 0.5), (byte)(Math.Pow(b, gamma) * 255 + 0.5));
		}


		// Get string.
		public string? GetString(string key, string? defaultValue = null)
		{
			if (this.Resources.TryGetResource($"String.{key}", out var value) && value is string str)
				return str;
			return defaultValue;
		}


		// Get system accent color.
		Color? GetSystemAccentColor()
		{
			if (this.windowsUISettings != null)
			{
				var color = this.windowsUISettings.GetColorValue(UIColorType.Accent);
				return Color.FromArgb(color.A, color.R, color.G, color.B);
			}
			return null;
		}


		// Initialize.
		public override void Initialize() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Check whether using system accent color is supported on current platform or not.
		/// </summary>
		public bool IsSystemAccentColorSupported { get; } = Global.Run(() =>
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return false;
			return Environment.OSVersion.Version >= new Version(10, 0, 17763);
		});


		/// <summary>
		/// Get Linux distribution on current running environment.
		/// </summary>
		public LinuxDistribution LinuxDistribution { get; private set; }


		/// <summary>
		/// Load settings from file asynchronously.
		/// </summary>
		/// <returns>True if settings has been loaded successfully.</returns>
		public async Task<bool> LoadSettingsAsync()
		{
			// check state
			this.VerifyAccess();
			var setting = this.settings;
			if (setting == null)
			{
				this.logger.LogError("No settings to load");
				return false;
			}

			// load settings
			try
			{
				this.logger.LogDebug("Start loading settings");
				await this.settingsFileAccessTaskFactory.StartNew(() => setting.Load(this.settingsFilePath));
				this.logger.LogDebug("Settings loaded");
				return true;
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Error occurred while loading settings");
				return false;
			}
		}


		// Program entry.
		[STAThread]
		static void Main(string[] args)
		{
			// start application
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

			// deinitialize application
			var app = App.Current;
			app?.Deinitialize();

			// restart
			if (app != null && app.isRestartRequested)
			{
				try
				{
					if (app.isRestartAsAdminRequested)
						app.logger.LogWarning("Restart as administrator/superuser");
					else
						app.logger.LogWarning("Restart");
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
					app.logger.LogError(ex, "Unable to restart");
				}
			}
		}


		// Called when framework initialized.
		public override async void OnFrameworkInitializationCompleted()
		{
			// call base
			base.OnFrameworkInitializationCompleted();

			// setup synchronization
			this.synchronizationContext = SynchronizationContext.Current ?? throw new ArgumentException("No SynchronizationContext when Avalonia initialized.");

			// create scheduled actions
			this.checkUpdateInfoAction = new ScheduledAction(() =>
			{
				_ = this.CheckUpdateInfoAsync();
			});
			this.updateProcessInfoAction = new ScheduledAction(async () =>
			{
				long privateMemoryUsage = 0;
				double cpuUsagePercentage = double.NaN;
				await Task.Run(() =>
				{
					var updateTime = this.stopWatch.ElapsedMilliseconds;
					try
					{
						this.process.Refresh();
						var totalProcessorTime = this.process.TotalProcessorTime;
						privateMemoryUsage = this.process.PrivateMemorySize64;
						if (this.previousProcessInfoUpdateTime > 0)
						{
							var processorTime = (totalProcessorTime - this.previousTotalProcessorTime);
							var updateInterval = (updateTime - this.previousProcessInfoUpdateTime);
							cpuUsagePercentage = (processorTime.TotalMilliseconds * 100.0 / updateInterval / Environment.ProcessorCount);
						}
						this.previousTotalProcessorTime = totalProcessorTime;
					}
					catch (Exception ex)
					{
						this.logger.LogError(ex, "Unable to get process info");
					}
					finally
					{
						this.previousProcessInfoUpdateTime = updateTime;
					}
				});
				if (!double.IsNaN(cpuUsagePercentage))
				{
					this.logger.LogTrace($"CPU usage: {cpuUsagePercentage:0.0}%");
					this.CpuUsagePercentage = cpuUsagePercentage;
					this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuUsagePercentage)));
				}
				if (privateMemoryUsage > 0)
				{
					this.logger.LogTrace($"Private memory usage: {privateMemoryUsage.ToFileSizeString()}");
					this.PrivateMemoryUsage = privateMemoryUsage;
					this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(PrivateMemoryUsage)));
				}
				this.updateProcessInfoAction?.Schedule(ProcessInfoUpdateInterval);
			});

			// parse startup params
			var desktopLifetime = (IClassicDesktopStyleApplicationLifetime)this.ApplicationLifetime;
			this.ParseStartupParams(desktopLifetime.Args);

			// enter debug mode
			if (this.StartupParams.LaunchInDebugMode)
			{
				this.logger.LogWarning("Enter debug mode");
				this.IsDebugMode = true;
#if !DEBUG
				NLog.LogManager.Configuration.AddRuleForAllLevels("methodCall");
#endif
			}
			else
			{
				NLog.LogManager.Configuration.RemoveRuleByName("logAllToFile");
				NLog.LogManager.Configuration.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, "file");
			}

			// show splash window
			var splashWindow = new SplashWindow();
			this.splashWindow = splashWindow;
			splashWindow.Show();

			// get initial process info
			this.updateProcessInfoAction.Execute();

			// load settings
			this.settings = new Settings();
			await this.LoadSettingsAsync();
			this.settings.SettingChanged += this.OnSettingChanged;

			// load persistent state
			this.persistentState = new Settings();
			this.logger.LogDebug("Start loading persistent state");
			try
			{
				await this.persistentState.LoadAsync(this.persistentStateFilePath);
				this.logger.LogDebug("Persistent state loaded");
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Failed to load persistent state");
			}

			// setup shutdown mode
			desktopLifetime.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

			// setup culture info
			this.UpdateCultureInfo();

			// load strings
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				this.Resources.MergedDictionaries.Add(new ResourceInclude()
				{
					Source = new Uri($"avares://ULogViewer/Strings/Default-Linux.axaml")
				});
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				this.Resources.MergedDictionaries.Add(new ResourceInclude()
				{
					Source = new Uri($"avares://ULogViewer/Strings/Default-OSX.axaml")
				});
			}
			this.UpdateStringResources();

			// attach to system UI settings
			if (this.IsSystemAccentColorSupported)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					this.windowsUISettings = new UISettings();
					this.windowsUISettings.ColorValuesChanged += this.OnWindowsUIColorValueChanged;
				}
			}

			// update styles
			splashWindow.Message = this.GetStringNonNull("SplashWindow.UpdateStyles");
			this.UpdateStyles();

			// initialize log data source providers
			splashWindow.Message = this.GetStringNonNull("SplashWindow.InitializeLogProfiles");
			LogDataSourceProviders.Initialize(this);

			// initialize log profiles
			await LogProfiles.InitializeAsync(this);

			// initialize predefined log text filters
			splashWindow.Message = this.GetStringNonNull("SplashWindow.InitializePredefinedLogTextFilters");
			await PredefinedLogTextFilters.InitializeAsync(this);

			// attach to system events
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				SystemEvents.UserPreferenceChanged += this.OnWindowsUserPreferenceChanged;

			// create workspace and restore state if needed
			splashWindow.Message = this.GetStringNonNull("SplashWindow.ShowMainWindow");
			this.workspace = new Workspace(this);
			var isWorkspaceStateRestored = this.StartupParams.IsRestoringStateRequested
				? this.workspace.RestoreState()
				: false;
			if (!isWorkspaceStateRestored)
			{
				var initialProfile = this.StartupParams.LogProfileId?.Let(it =>
				{
					if (LogProfiles.TryFindProfileById(it, out var profile))
					{
						this.logger.LogWarning($"Initial log profile is '{profile?.Name}'");
						return profile;
					}
					this.logger.LogError($"Cannot find initial log profile by ID '{it}'");
					return null;
				}) ?? this.Settings.GetValueOrDefault(Settings.InitialLogProfile).Let(it =>
				{
					if (string.IsNullOrEmpty(it))
						return null;
					if (LogProfiles.TryFindProfileById(it, out var profile))
					{
						this.logger.LogWarning($"Initial log profile is '{profile?.Name}'");
						return profile;
					}
					this.logger.LogError($"Cannot find initial log profile by ID '{it}'");
					return null;
				});
				if (initialProfile != null)
					this.workspace.CreateSession(initialProfile);
				this.workspace.ClearSavedState();
			}

			// start checking update
			_ = this.CheckUpdateInfoAsync();

			// show main window
			this.synchronizationContext.Post(this.ShowMainWindow);
		}


		// Called when main window closed.
		async void OnMainWindowClosed()
		{
			this.logger.LogWarning("Main window closed");

			// detach from main window
			this.mainWindow = this.mainWindow?.Let((it) =>
			{
				it.DataContext = null;
				return (MainWindow?)null;
			});

			// save settings
			await this.SaveSettingsAsync();

			// save instance state
			this.workspace?.SaveState();

			// save persistent state
			this.logger.LogDebug("Start saving persistent state");
			try
			{
				await this.persistentState.AsNonNull().SaveAsync(this.persistentStateFilePath);
				this.logger.LogDebug("Persistent state saved");
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Failed to save persistent state");
			}

			// save predefined log text filters
			await PredefinedLogTextFilters.SaveAllAsync();

			// wait for IO completion of log profiles
			await LogProfiles.WaitForIOCompletionAsync();

			// wait for necessary tasks
			if (this.workspace != null)
				await this.workspace.WaitForNecessaryTasksAsync();

			// shutdown application
			this.Shutdown();
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == Settings.Culture)
				this.UpdateCultureInfo();
			else if (e.Key == Settings.ThemeMode
				|| e.Key == Settings.UseSystemAccentColor)
			{
				this.UpdateStyles();
			}
		}


		// Called when system accent color changed.
		void OnSystemAccentColorChanged() => this.UpdateStyles();


		// Called when system culture info has been changed.
		void OnSystemCultureInfoChanged()
		{
			this.logger.LogWarning("Culture info of system has been changed");

			// update culture info
			if (this.Settings.GetValueOrDefault(Settings.Culture) == AppCulture.System)
				this.UpdateCultureInfo();
		}


		// Called when Windows UI color changed.
		void OnWindowsUIColorValueChanged(UISettings sender, object result) => this.SynchronizationContext.Post(this.OnSystemAccentColorChanged);


		// Called when user preference changed on Windows
		void OnWindowsUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
		{
			if (e.Category == UserPreferenceCategory.Locale)
				this.SynchronizationContext.Post(this.OnSystemCultureInfoChanged);
		}


		// Parse startup parameters.
		void ParseStartupParams(string[] args)
		{
#if DEBUG
			var isDebugMode = true;
#else
			var isDebugMode = false;
#endif
			var isRestoringStateRequested = false;
			var logProfileId = (string?)null;
			for (int i = 0, count = args.Length; i < count; ++i)
			{
				switch(args[i])
				{
					case "-debug":
						isDebugMode = true;
						break;
					case "-profile":
						if (i < count - 1)
							logProfileId = args[++i];
						else
							this.logger.LogError("ID of initial log profile is not specified");
						break;
					case "-restore-state":
						isRestoringStateRequested = true;
						break;
					default:
						this.logger.LogWarning($"Unknown argument: {args[i]}");
						break;
				}
			}
			this.StartupParams = new AppStartupParams()
			{
				IsRestoringStateRequested = isRestoringStateRequested,
				LaunchInDebugMode = isDebugMode,
				LogProfileId = logProfileId
			};
		}


		/// <summary>
		/// Get private memory usage by application in bytes.
		/// </summary>
		public long PrivateMemoryUsage { get; private set; }


		// Restart application.
		public bool Restart(string? args, bool asAdministrator)
		{
			// check state
			this.VerifyAccess();
			if (this.isRestartRequested)
			{
				if (!string.IsNullOrEmpty(args) && !string.IsNullOrEmpty(this.restartArgs) && args != this.restartArgs)
				{
					this.logger.LogError("Try restarting application with different arguments");
					return false;
				}
				this.isRestartAsAdminRequested |= asAdministrator;
				this.restartArgs = args;
				if (this.isRestartAsAdminRequested)
					this.logger.LogWarning("Already restarting as administrator/superuser");
				else
					this.logger.LogWarning("Already restarting");
				return true;
			}

			// update state
			this.isRestartRequested = true;
			this.isRestartAsAdminRequested = asAdministrator;
			this.restartArgs = args;
			if (asAdministrator)
				this.logger.LogWarning("Request restarting as administrator/superuser");
			else
				this.logger.LogWarning("Request restarting");

			// close main window or shutdown
			if (this.mainWindow != null)
			{
				this.logger.LogWarning("Schedule closing main window to restart");
				this.SynchronizationContext.Post(() => this.mainWindow?.Close());
			}
			else
			{
				this.logger.LogWarning("Schedule shutdown to restart");
				this.SynchronizationContext.Post(this.Shutdown);
			}
			return true;
		}


		/// <summary>
		/// Start saving settings asynchronously.
		/// </summary>
		/// <returns>True if settings saved successfully.</returns>
		public async Task<bool> SaveSettingsAsync()
		{
			// check state
			this.VerifyAccess();
			var setting = this.settings;
			if (setting == null)
			{
				this.logger.LogError("No settings to save");
				return false;
			}

			// save settings
			try
			{
				this.logger.LogDebug("Start saving settings");
				await this.settingsFileAccessTaskFactory.StartNew(() => setting.Save(this.settingsFilePath));
				this.logger.LogDebug("Settings saved");
				return true;
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Error occurred while saving settings");
				return false;
			}
		}


		/// <summary>
		/// Get application settings.
		/// </summary>
		public Settings Settings { get => this.settings ?? throw new InvalidOperationException("Application is not ready."); }


		// Create and show main window.
		void ShowMainWindow()
		{
			// check state
			if (this.mainWindow != null)
			{
				this.logger.LogError("Already shown main window");
				return;
			}

			// show main window
			this.mainWindow = new MainWindow().Also((it) =>
			{
				it.Closed += (_, e) => this.OnMainWindowClosed();
				it.DataContext = this.workspace;
			});
			this.logger.LogWarning("Show main window");
			this.mainWindow.Show();

			// close splash window
			this.splashWindow = this.splashWindow?.Let(it =>
			{
				it.Close();
				return (SplashWindow?)null;
			});
		}


		/// <summary>
		/// Shutdown application.
		/// </summary>
		public void Shutdown()
		{
			this.logger.LogWarning("Shutdown");
			this.VerifyAccess();
			(this.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
		}


		// Update culture info according to current culture info and settings.
		void UpdateCultureInfo()
		{
			// select culture info
			var cultureInfo = this.Settings.GetValueOrDefault(Settings.Culture).Let(it =>
			{
				if (it == AppCulture.System)
					return CultureInfo.CurrentCulture;
				var name = new StringBuilder(it.ToString());
				for (var i = 0; i < name.Length; ++i)
				{
					var c = name[i];
					if (c == '_')
					{
						name[i] = '-';
						break;
					}
					name[i] = char.ToLower(c);
				}
				try
				{
					return CultureInfo.GetCultureInfo(name.ToString());
				}
				catch
				{
					logger.LogError($"Unknown culture: {name}");
					return CultureInfo.CurrentCulture;
				}
			});
			cultureInfo.ClearCachedData();

			// check current culture info
			if (this.CultureInfo.Equals(cultureInfo))
				return;

			logger.LogWarning($"Update application culture to {cultureInfo.Name}");

			// change culture info
			this.CultureInfo = cultureInfo;
			this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(CultureInfo)));

			// update string resources
			this.UpdateStringResources();
		}


		// Update dynamic font families according to current culture info and settings.
		void UpdateDynamicFontFamilies()
		{
			var fontFamily = Global.Run(() =>
			{
				if (!this.Resources.TryGetResource("String.FallbackFontFamilies", out var res) || res is not string fontFamilies)
					return null;
				return $"{fontFamilies}";
			})?.Let(it => new FontFamily(it));
			if (fontFamily != null)
				this.Resources[TextBoxFontFamilyResourceKey] = fontFamily;
			else
				this.Resources.Remove(TextBoxFontFamilyResourceKey);
		}


		// Update string resources according to current culture info and settings.
		void UpdateStringResources()
		{
			var updated = false;
			var cultureInfo = this.CultureInfo;
			if (cultureInfo.Name != "en-US")
			{
				// clear resources
				if (!cultureInfo.Equals(this.stringResourcesCulture))
				{
					if (this.stringResources != null)
					{
						this.Resources.MergedDictionaries.Remove(this.stringResources);
						this.stringResources = null;
						updated = true;
					}
					if (this.stringResourcesForOS != null)
					{
						this.Resources.MergedDictionaries.Remove(this.stringResourcesForOS);
						this.stringResourcesForOS = null;
						updated = true;
					}
				}

				// base resources
				if (this.stringResources == null)
				{
					try
					{
						this.stringResources = new ResourceInclude()
						{
							Source = new Uri($"avares://ULogViewer/Strings/{cultureInfo.Name}.axaml")
						};
						_ = this.stringResources.Loaded; // trigger error if resource not found
						this.logger.LogInformation($"Load strings for {cultureInfo.Name}");
					}
					catch
					{
						this.stringResources = null;
						this.logger.LogWarning($"No strings for {cultureInfo.Name}");
						return;
					}
					this.Resources.MergedDictionaries.Add(this.stringResources);
					updated = true;
				}
				else if (!this.Resources.MergedDictionaries.Contains(this.stringResources))
				{
					this.Resources.MergedDictionaries.Add(this.stringResources);
					updated = true;
				}

				// resources for specific OS
				var osName = Global.Run(() =>
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
						return "Linux";
					if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
						return "OSX";
					return null;
				});
				if (osName != null)
				{
					if (this.stringResourcesForOS == null)
					{
						try
						{
							this.stringResourcesForOS = new ResourceInclude()
							{
								Source = new Uri($"avares://ULogViewer/Strings/{cultureInfo.Name}-{osName}.axaml")
							};
							_ = this.stringResourcesForOS.Loaded; // trigger error if resource not found
							this.logger.LogInformation($"Load strings ({osName}) for {cultureInfo.Name}.");
						}
						catch
						{
							this.stringResourcesForOS = null;
							this.logger.LogWarning($"No strings ({osName}) for {cultureInfo.Name}.");
						}
						if (this.stringResourcesForOS != null)
						{
							this.Resources.MergedDictionaries.Add(this.stringResourcesForOS);
							updated = true;
						}
					}
					else if (!this.Resources.MergedDictionaries.Contains(this.stringResourcesForOS))
					{
						this.Resources.MergedDictionaries.Add(this.stringResourcesForOS);
						updated = true;
					}
				}
			}
			else
			{
				if (this.stringResources != null)
					updated |= this.Resources.MergedDictionaries.Remove(this.stringResources);
				if (this.stringResourcesForOS != null)
					updated |= this.Resources.MergedDictionaries.Remove(this.stringResourcesForOS);
			}
			if (updated)
			{
				this.logger.LogWarning("String resources updated");
				this.stringResourcesCulture = cultureInfo;
				this.UpdateDynamicFontFamilies();
				this.StringsUpdated?.Invoke(this, EventArgs.Empty);
			}
		}


		// Update styles according to settings.
		void UpdateStyles()
		{
			// check current styles
			var themeMode = this.Settings.GetValueOrDefault(Settings.ThemeMode);
			var stylesToRemove = (StyleInclude?)null;
			if (this.stylesThemeMode != themeMode && this.styles != null)
			{
				stylesToRemove = this.styles;
				this.styles = null;
			}

			// update styles
			if (this.styles == null)
			{
				this.styles = new StyleInclude(new Uri("avares://ULogViewer/"))
				{
					Source = new Uri($"avares://ULogViewer/Styles/{themeMode}.axaml")
				};
				this.Styles.Add(this.styles);
			}
			else if (!this.Styles.Contains(this.styles))
				this.Styles.Add(this.styles);
			this.stylesThemeMode = themeMode;

			// update system accent color
			if (this.Settings.GetValueOrDefault(Settings.UseSystemAccentColor))
			{
				if (this.systemAccentColorResources == null)
					this.systemAccentColorResources = new ResourceDictionary();
				this.GetSystemAccentColor()?.Let(sysAccentColor =>
				{
					var sysAccentColorDark1 = GammaTransform(sysAccentColor, 2.8);
					var sysAccentColorLight1 = GammaTransform(sysAccentColor, 0.682);
					this.systemAccentColorResources["SystemAccentColor"] = sysAccentColor;
					this.systemAccentColorResources["SystemAccentColorDark1"] = sysAccentColorDark1;
					this.systemAccentColorResources["SystemAccentColorDark2"] = GammaTransform(sysAccentColor, 4.56);
					this.systemAccentColorResources["SystemAccentColorDark3"] = GammaTransform(sysAccentColor, 5.365);
					this.systemAccentColorResources["SystemAccentColorLight1"] = sysAccentColorLight1;
					this.systemAccentColorResources["SystemAccentColorLight2"] = GammaTransform(sysAccentColor, 0.431);
					this.systemAccentColorResources["SystemAccentColorLight3"] = GammaTransform(sysAccentColor, 0.006);
					// [Workaround] Brushes of ToggleSwitch
					this.systemAccentColorResources["ToggleSwitchFillOnPointerOver"] = new SolidColorBrush(sysAccentColorLight1);
					this.systemAccentColorResources["ToggleSwitchFillOnPressed"] = new SolidColorBrush(sysAccentColorDark1);
					this.systemAccentColorResources["ToggleSwitchStrokeOnPointerOver"] = new SolidColorBrush(sysAccentColorLight1);
					this.systemAccentColorResources["ToggleSwitchStrokeOnPressed"] = new SolidColorBrush(sysAccentColorDark1);
				});
				if (!this.Resources.MergedDictionaries.Contains(this.systemAccentColorResources))
					this.Resources.MergedDictionaries.Add(this.systemAccentColorResources);
			}
			else if (this.systemAccentColorResources != null)
				this.Resources.MergedDictionaries.Remove(this.systemAccentColorResources);

			// remove styles
			if (stylesToRemove != null)
				this.Styles.Remove(stylesToRemove);
		}


		// Interface implementations.
		public Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
		public CultureInfo CultureInfo { get; private set; } = CultureInfo.CurrentCulture;
		public bool IsDebugMode { get; private set; }
		public bool IsRunningAsAdministrator { get; private set; }
		public bool IsShutdownStarted { get; private set; }
		public bool IsTesting => false;
		public ILoggerFactory LoggerFactory => new LoggerFactory(new ILoggerProvider[] { new NLogLoggerProvider() });
		public ISettings PersistentState { get => this.persistentState ?? throw new InvalidOperationException("Application is not ready."); }
		event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
		{
			add => this.propertyChangedHandlers += value;
			remove => this.propertyChangedHandlers -= value;
		}
		public string RootPrivateDirectoryPath { get; }
		ISettings CarinaStudio.IApplication.Settings { get => this.Settings; }
		public AppStartupParams StartupParams { get; private set; }
		public event EventHandler? StringsUpdated;
		public SynchronizationContext SynchronizationContext { get => this.synchronizationContext ?? throw new InvalidOperationException("Application is not ready."); }
		public AppUpdateInfo? UpdateInfo { get => this.updateInfo; }
	}
}
