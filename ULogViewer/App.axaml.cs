using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
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
using System.Text;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application.
	/// </summary>
	class App : Application, IApplication
	{
		// Constants.
		const string TextBoxFontFamilyResourceKey = "FontFamily.TextBox";


		// Fields.
		readonly ILogger logger;
		MainWindow? mainWindow;
		PropertyChangedEventHandler? propertyChangedHandlers;
		volatile Settings? settings;
		readonly string settingsFilePath;
		ResourceInclude? stringResources;
		CultureInfo? stringResourcesCulture;
		ResourceInclude? stringResourcesLinux;
		StyleInclude? styles;
		ThemeMode? stylesThemeMode;
		volatile SynchronizationContext? synchronizationContext;
		Workspace? workspace;


		// Constructor.
		public App()
		{
			// setup logger
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

			// prepare file path of settings
			this.settingsFilePath = Path.Combine(this.RootPrivateDirectoryPath, "Settings.json");
		}


		// Avalonia configuration, don't remove; also used by visual designer.
		static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.UseReactiveUI()
			.LogToTrace();


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

			this.logger.LogWarning("Stop");
		}


		// Get string.
		public string? GetString(string key, string? defaultValue = null)
		{
			if (this.Resources.TryGetResource($"String.{key}", out var value) && value is string str)
				return str;
			return defaultValue;
		}


		// Initialize.
		public override void Initialize() => AvaloniaXamlLoader.Load(this);


		// Program entry.
		[STAThread]
		static void Main(string[] args)
		{
			// start application
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

			// deinitialize application
			App.Current?.Deinitialize();
		}


		// Called when framework initialized.
		public override async void OnFrameworkInitializationCompleted()
		{
			// call base
			base.OnFrameworkInitializationCompleted();

			// setup synchronization
			this.synchronizationContext = SynchronizationContext.Current ?? throw new ArgumentException("No SynchronizationContext when Avalonia initialized.");

			// load settings
			this.settings = new Settings();
			this.logger.LogDebug("Start loading settings");
			try
			{
				await this.settings.LoadAsync(this.settingsFilePath);
				this.logger.LogDebug("Settings loaded");
			}
			catch (Exception ex)
			{
				this.logger.LogWarning(ex, "Unable to load settings");
			}
			this.settings.SettingChanged += this.OnSettingChanged;

			// setup shutdown mode
			if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
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
			this.UpdateStringResources();

			// initialize log data source providers
			LogDataSourceProviders.Initialize(this);

			// initialize log profiles
			await LogProfiles.InitializeAsync(this);

			// initialize predefined log text filters
			await PredefinedLogTextFilters.InitializeAsync(this);

			// attach to system events
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				SystemEvents.UserPreferenceChanged += this.OnWindowsUserPreferenceChanged;

			// create workspace
			this.workspace = new Workspace(this);

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
			this.logger.LogDebug("Start saving settings");
			try
			{
				await this.Settings.SaveAsync(this.settingsFilePath);
				this.logger.LogDebug("Settings saved");
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Unable to save settings");
			}

			// save predefined log text filters
			await PredefinedLogTextFilters.SaveAllAsync();

			// wait for IO completion of log profiles
			await LogProfiles.WaitForIOCompletionAsync();

			// restart main window
			//

			// shutdown application
			if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
			{
				this.logger.LogWarning("Shutdown");
				desktopLifetime.Shutdown();
			}
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == Settings.Culture)
				this.UpdateCultureInfo();
			else if (e.Key == Settings.ThemeMode)
				this.UpdateStyles();
		}


		// Called when system culture info has been changed.
		void OnSystemCultureInfoChanged()
		{
			this.logger.LogWarning("Culture info of system has been changed");

			// update culture info
			if (this.Settings.GetValueOrDefault(Settings.Culture) == AppCulture.System)
				this.UpdateCultureInfo();
		}


#pragma warning disable CA1416
		// Called when user preference changed on Windows
		void OnWindowsUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
		{
			if (e.Category == UserPreferenceCategory.Locale)
				this.OnSystemCultureInfoChanged();
		}
#pragma warning restore CA1416


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

			// check application update
			//

			// update styles
			this.UpdateStyles();

			// show main window
			this.mainWindow = new MainWindow().Also((it) =>
			{
				it.Closed += (_, e) => this.OnMainWindowClosed();
				it.DataContext = this.workspace;
			});
			this.logger.LogWarning("Show main window");
			this.mainWindow.Show();
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
					if (this.stringResourcesLinux != null)
					{
						this.Resources.MergedDictionaries.Remove(this.stringResourcesLinux);
						this.stringResourcesLinux = null;
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
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					if (this.stringResourcesLinux == null)
					{
						try
						{
							this.stringResourcesLinux = new ResourceInclude()
							{
								Source = new Uri($"avares://ULogViewer/Strings/{cultureInfo.Name}-Linux.axaml")
							};
							_ = this.stringResourcesLinux.Loaded; // trigger error if resource not found
							this.logger.LogInformation($"Load strings (Linux) for {cultureInfo.Name}.");
						}
						catch
						{
							this.stringResourcesLinux = null;
							this.logger.LogWarning($"No strings (Linux) for {cultureInfo.Name}.");
							return;
						}
						this.Resources.MergedDictionaries.Add(this.stringResourcesLinux);
						updated = true;
					}
					else if (!this.Resources.MergedDictionaries.Contains(this.stringResourcesLinux))
					{
						this.Resources.MergedDictionaries.Add(this.stringResourcesLinux);
						updated = true;
					}
				}
			}
			else
			{
				if (this.stringResources != null)
					updated |= this.Resources.MergedDictionaries.Remove(this.stringResources);
				if (this.stringResourcesLinux != null)
					updated |= this.Resources.MergedDictionaries.Remove(this.stringResourcesLinux);
			}
			if (updated)
			{
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

			// remove styles
			if (stylesToRemove != null)
				this.Styles.Remove(stylesToRemove);
		}


		// Interface implementations.
		public Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
		public CultureInfo CultureInfo { get; private set; } = CultureInfo.CurrentCulture;
		public bool IsShutdownStarted { get; private set; }
		public bool IsTesting => false;
		public ILoggerFactory LoggerFactory => new LoggerFactory(new ILoggerProvider[] { new NLogLoggerProvider() });
		event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
		{
			add => this.propertyChangedHandlers += value;
			remove => this.propertyChangedHandlers -= value;
		}
		public string RootPrivateDirectoryPath => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? throw new ArgumentException("Unable to get directory of application.");
		BaseSettings CarinaStudio.IApplication.Settings { get => this.Settings; }
		public event EventHandler? StringsUpdated;
		public SynchronizationContext SynchronizationContext { get => this.synchronizationContext ?? throw new InvalidOperationException("Application is not ready."); }
	}
}
