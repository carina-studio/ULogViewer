using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
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
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application.
	/// </summary>
	class App : Application, IApplication
	{
		// Fields.
		readonly ILogger logger;
		MainWindow? mainWindow;
		PropertyChangedEventHandler? propertyChangedHandlers;
		volatile Settings? settings;
		readonly string settingsFilePath;
		ResourceInclude? stringResources;
		ResourceInclude? stringResourcesLinux;
		volatile SynchronizationContext? synchronizationContext;


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

			// attach to system events
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				SystemEvents.UserPreferenceChanged += this.OnWindowsUserPreferenceChanged;

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
			if (e.Key == ULogViewer.Settings.SelectLanguageAutomatically)
				this.UpdateStringResources();
		}


		// Called when system culture info has been changed.
		void OnSystemCultureInfoChanged()
		{
			this.logger.LogWarning("Culture info of system has been changed");

			// update culture info
			this.CultureInfo = CultureInfo.CurrentCulture;
			this.CultureInfo.ClearCachedData();
			this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(CultureInfo)));

			// update string resources
			if (this.stringResources != null)
			{
				this.Resources.MergedDictionaries.Remove(this.stringResources);
				this.stringResources = null;
			}
			if (this.stringResourcesLinux != null)
			{
				this.Resources.MergedDictionaries.Remove(this.stringResourcesLinux);
				this.stringResourcesLinux = null;
			}
			this.UpdateStringResources();
		}


#pragma warning disable CA1416
		// Called when user preference changed on Windows
		void OnWindowsUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
		{
			if (e.Category == UserPreferenceCategory.Locale)
				this.OnSystemCultureInfoChanged();
		}
#pragma warning restore CA1416


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
			//

			// show main window
			this.mainWindow = new MainWindow().Also((it) =>
			{
				it.Closed += (_, e) => this.OnMainWindowClosed();
			});
			this.logger.LogWarning("Show main window");
			this.mainWindow.Show();
		}


		// Update string resources according to current culture info and settings.
		void UpdateStringResources()
		{
			var updated = false;
			if (this.Settings.GetValueOrDefault(ULogViewer.Settings.SelectLanguageAutomatically))
			{
				// base resources
				var localeName = this.CultureInfo.Name;
				if (this.stringResources == null)
				{
					try
					{
						this.stringResources = new ResourceInclude()
						{
							Source = new Uri($"avares://ULogViewer/Strings/{localeName}.axaml")
						};
						_ = this.stringResources.Loaded; // trigger error if resource not found
						this.logger.LogInformation($"Load strings for {localeName}");
					}
					catch
					{
						this.stringResources = null;
						this.logger.LogWarning($"No strings for {localeName}");
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
								Source = new Uri($"avares://ULogViewer/Strings/{localeName}-Linux.axaml")
							};
							_ = this.stringResourcesLinux.Loaded; // trigger error if resource not found
							this.logger.LogInformation($"Load strings (Linux) for {localeName}.");
						}
						catch
						{
							this.stringResourcesLinux = null;
							this.logger.LogWarning($"No strings (Linux) for {localeName}.");
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
				this.StringsUpdated?.Invoke(this, EventArgs.Empty);
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
		public BaseSettings Settings { get => this.settings ?? throw new InvalidOperationException("Application is not ready."); }
		public event EventHandler? StringsUpdated;
		public SynchronizationContext SynchronizationContext { get => this.synchronizationContext ?? throw new InvalidOperationException("Application is not ready."); }
	}
}
