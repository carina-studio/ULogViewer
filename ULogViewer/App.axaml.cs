using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Markup.Xaml;
using CarinaStudio;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
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
		volatile Settings? settings;
		readonly string settingsFilePath;
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


		// Get string.
		public string? GetString(string key, string? defaultValue = null) => defaultValue;


		// Initialize.
		public override void Initialize() => AvaloniaXamlLoader.Load(this);


		// Program entry.
		static void Main(string[] args)
		{
			// start application
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

			// stop
			App.Current?.logger?.LogWarning("Stop");
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

			// setup shutdown mode
			if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
				desktopLifetime.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

			// initialize log data source providers
			LogDataSourceProviders.Initialize(this);

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


		// Interface implementations.
		public bool IsShutdownStarted { get; private set; }
		public ILoggerFactory LoggerFactory => new LoggerFactory(new ILoggerProvider[] { new NLogLoggerProvider() });
		public string RootPrivateDirectoryPath => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? throw new ArgumentException("Unable to get directory of application.");
		public BaseSettings Settings { get => this.settings ?? throw new InvalidOperationException("Application is not ready."); }
		public SynchronizationContext SynchronizationContext { get => this.synchronizationContext ?? throw new InvalidOperationException("Application is not ready."); }
	}
}
