using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CarinaStudio.AutoUpdater
{
	/// <summary>
	/// Application.
	/// </summary>
	class App : Application
	{
		// Fields.
		//


		// Constructor.
		public App()
		{ }


		/// <summary>
		/// Get root directory of application to update.
		/// </summary>
		public string? ApplicationDirectoryPath { get; private set; }


		/// <summary>
		/// Get name of application to update.
		/// </summary>
		public string? ApplicationName { get; private set; }


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


		// Program entry.
		[STAThread]
		static void Main(string[] args)
		{
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}


		// Called when framework initialized.
		public override void OnFrameworkInitializationCompleted()
		{
			// call base
			base.OnFrameworkInitializationCompleted();

			// parse arguments
			var desktopLifetime = (IClassicDesktopStyleApplicationLifetime)this.ApplicationLifetime;
			var args = desktopLifetime.Args;
			for (var i = 0; i < args.Length; ++i)
			{
				switch (args[i])
				{
					case "-d":
						if (i < args.Length - 1)
							this.ApplicationDirectoryPath = args[++i];
						break;
					case "-n":
						if (i < args.Length - 1)
							this.ApplicationName = args[++i];
						break;
					case "-p":
						if (i < args.Length - 1 && Uri.TryCreate(args[++i], UriKind.Absolute, out var uri))
							this.UpdatePackageUri = uri;
						break;
				}
			}

			// check arguments
			if (string.IsNullOrWhiteSpace(this.ApplicationDirectoryPath) || this.UpdatePackageUri == null)
			{
				SynchronizationContext.Current?.Post(() => desktopLifetime.Shutdown());
				return;
			}

			// start updating
			//
		}


		/// <summary>
		/// Get URI of application update package to download.
		/// </summary>
		public Uri? UpdatePackageUri { get; private set; }
	}
}
