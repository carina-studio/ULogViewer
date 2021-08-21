using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace CarinaStudio.AutoUpdater
{
	/// <summary>
	/// Application.
	/// </summary>
	class App : Application, INotifyPropertyChanged
	{
		// Fields.
		volatile bool isCancellationRequested;
		PropertyChangedEventHandler? propertyChangedHandlers;
		SynchronizationContext? syncContext;


		// Constructor.
		public App()
		{ }


		/// <summary>
		/// Get root directory of application to update.
		/// </summary>
		public string? ApplicationDirectoryPath { get; private set; }


		/// <summary>
		/// Get executable path of application to update.
		/// </summary>
		public string? ApplicationExecutablePath { get; private set; }


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


		// Cancel.
		public void Cancel()
		{
			if (!this.IsCancellable)
				return;
			this.IsCancellable = false;
			this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCancellable)));
			this.isCancellationRequested = true;
			this.UpdateMessage("Cancelling...");
		}


		// Copy files between directories.
		void CopyFiles(string srcDirectory, string destDirectory, bool isCancellable, bool throwException)
		{
			var srcDirectories = new Queue<string>();
			srcDirectories.Enqueue(srcDirectory);
			while (srcDirectories.TryDequeue(out var srcSubDirectory) && srcSubDirectory != null)
			{
				// find sub directories
				try
				{
					foreach (var path in Directory.EnumerateDirectories(srcSubDirectory))
					{
						if (path != destDirectory && !Path.GetFileName(path).StartsWith("AutoUpdte-"))
							srcDirectories.Enqueue(path);
					}
				}
				catch
				{
					if (throwException)
						throw;
				}

				// cancellation check
				if (isCancellable && this.isCancellationRequested)
				{
					if (throwException)
						throw new Exception("Updating has been cancelled.");
					return;
				}

				// create destination sub directory
				var destSubDirectory = "";
				if (srcSubDirectory != srcDirectory)
				{
					destSubDirectory = Path.Combine(destDirectory, Path.GetRelativePath(srcDirectory, srcSubDirectory));
					try
					{
						Directory.CreateDirectory(destSubDirectory);
					}
					catch
					{
						if (throwException)
							throw;
						continue;
					}
				}
				else
					destSubDirectory = destDirectory;

				// copy files
				try
				{
					foreach (var srcFilePath in Directory.EnumerateFiles(srcSubDirectory))
					{
						try
						{
							// copy file
							var destFilePath = Path.Combine(destSubDirectory, Path.GetFileName(srcFilePath));
							File.Copy(srcFilePath, destFilePath, true);

							// cancellation check
							if (isCancellable && this.isCancellationRequested)
								throw new Exception("Updating has been cancelled.");
						}
						catch
						{
							if (throwException)
								throw;
							if (isCancellable && this.isCancellationRequested)
								return;
						}
					}
				}
				catch
				{
					if (throwException)
						throw;
				}
			}
		}


		/// <summary>
		/// Get <see cref="App"/> instance for current process.
		/// </summary>
		public static new App Current
		{
			get => (App)Application.Current;
		}


		/// <summary>
		/// Check whether <see cref="ProgressPercentage"/> is not <see cref="double.NaN"/> or not.
		/// </summary>
		public bool HasProgress { get; private set; }


		// Initialize.
		public override void Initialize() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Check whether auto updating is now cancellable or not.
		/// </summary>
		public bool IsCancellable { get; private set; }


		/// <summary>
		/// Check whether auto updating is completed or not.
		/// </summary>
		public bool IsCompleted { get; private set; }


		// Program entry.
		[STAThread]
		static void Main(string[] args)
		{
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}


		/// <summary>
		/// Get message of current state.
		/// </summary>
		public string? Message { get; private set; }


		// Called when framework initialized.
		public override void OnFrameworkInitializationCompleted()
		{
			// call base
			base.OnFrameworkInitializationCompleted();

			// get synchronization context
			this.syncContext = SynchronizationContext.Current;

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
					case "-e":
						if (i < args.Length - 1)
							this.ApplicationExecutablePath = args[++i];
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
				this.syncContext?.Post(() => desktopLifetime.Shutdown());
				return;
			}
			if (string.IsNullOrWhiteSpace(this.ApplicationName))
				this.ApplicationName = "Application";

			// start updating
			this.IsCancellable = true;
			ThreadPool.QueueUserWorkItem(_ => this.UpdateProc());
			new MainWindow().Show();
		}


		// Called when updating completed.
		void OnUpdateCompleted(Exception? exception)
		{
			if (exception == null || this.isCancellationRequested)
			{
				if (this.isCancellationRequested)
					this.UpdateMessage($"Updating has been cancelled.");
				else
					this.UpdateMessage($"{this.ApplicationName} update completed.");
				this.ApplicationExecutablePath.Let(exePath =>
				{
					if (!string.IsNullOrWhiteSpace(exePath))
					{
						try
						{
							using var process = new Process().Also(process =>
							{
								process.StartInfo.FileName = exePath;
							});
							process.Start();
							((IClassicDesktopStyleApplicationLifetime)this.ApplicationLifetime).Shutdown();
						}
						catch
						{ }
					}
				});
			}
			else
			{
				if (this.IsCancellable)
				{
					this.IsCancellable = false;
					this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCancellable)));
				}
				this.UpdateMessage($"Failed to update {this.ApplicationName}.");
			}
			this.IsCompleted = true;
			this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
		}


		/// <summary>
		/// Get progress of auto updating in percentage.
		/// </summary>
		public double ProgressPercentage { get; private set; } = double.NaN;


		/// <summary>
		/// Get URI of application update package to download.
		/// </summary>
		public Uri? UpdatePackageUri { get; private set; }


		// Update message.
		void UpdateMessage(string? message)
		{
			this.syncContext?.Post(() =>
			{
				this.Message = message;
				this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
			});
		}


		// Procedure of auto updating in background thread.
		void UpdateProc()
		{
			this.UpdateMessage("Preparing...");
			var exception = (Exception?)null;
			var tempDirectoryPath = "";
			var appDirectoryPath = this.ApplicationDirectoryPath.AsNonNull();
			var appBackupDirectoryPath = "";
			try
			{
				// create temporary directory
				tempDirectoryPath = Path.Combine(Path.GetTempPath(), $"CarinaStudio-AutoUpdte-{DateTime.Now.ToBinary()}");
				var tempDirectory = Directory.CreateDirectory(tempDirectoryPath);

				// backup current application files
				this.UpdateMessage($"Backup {this.ApplicationName}...");
				appBackupDirectoryPath = Path.Combine(appDirectoryPath, $"AutoUpdte-Backup-{DateTime.Now.ToBinary()}");
				var appBackupDirectory = Directory.CreateDirectory(appBackupDirectoryPath);
				this.CopyFiles(appDirectoryPath, appBackupDirectoryPath, true, true);

				// cancellation check
				if (this.isCancellationRequested)
					return;

				// download update package
				this.UpdateMessage("Downloading update package...");
				using var webResponse = WebRequest.Create(this.UpdatePackageUri.AsNonNull()).GetResponse();
				using var webStream = webResponse.GetResponseStream();
				var updatePackagePath = Path.Combine(tempDirectoryPath, Path.GetFileName(webResponse.ResponseUri.LocalPath));
				var updatePackageSize = webResponse.ContentLength;
				var downloadedSize = 0L;
				if (updatePackageSize > 0)
					this.UpdateProgressPercentage(0);
				using (var fileStream = new FileStream(updatePackagePath, FileMode.Create, FileAccess.Write))
				{
					var buffer = new byte[4096];
					var readCount = webStream.Read(buffer, 0, buffer.Length);
					while (readCount > 0)
					{
						downloadedSize += readCount;
						if (updatePackageSize > 0)
							this.UpdateProgressPercentage((downloadedSize * 100.0) / updatePackageSize);
						fileStream.Write(buffer, 0, readCount);
						readCount = webStream.Read(buffer, 0, buffer.Length);
					}
				}

				// extract and update application files
				this.UpdateMessage($"Updating {this.ApplicationName}...");
				this.UpdateProgressPercentage(double.NaN);
				ZipFile.ExtractToDirectory(updatePackagePath, appDirectoryPath, true);
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			finally
			{
				// restore application files
				if (exception != null || this.isCancellationRequested)
				{
					if (!string.IsNullOrWhiteSpace(appBackupDirectoryPath))
					{
						this.UpdateMessage($"Restoring {this.ApplicationName}...");
						this.UpdateProgressPercentage(double.NaN);
						this.CopyFiles(appBackupDirectoryPath, appDirectoryPath, false, false);
					}
				}

				// delete temporary directories
				this.UpdateMessage("Completing...");
				Global.RunWithoutError(() =>
				{
					if (!string.IsNullOrWhiteSpace(tempDirectoryPath))
						Directory.Delete(tempDirectoryPath, true);
				});
				Global.RunWithoutError(() =>
				{
					if (!string.IsNullOrWhiteSpace(appBackupDirectoryPath))
						Directory.Delete(appBackupDirectoryPath, true);
				});

				// complete
				this.syncContext?.Post(() => this.OnUpdateCompleted(exception));
			}
		}


		// Update progress.
		void UpdateProgressPercentage(double progress)
		{
			this.syncContext?.Post(() =>
			{
				this.ProgressPercentage = progress;
				this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressPercentage)));
				this.HasProgress = double.IsFinite(progress);
				this.propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(HasProgress)));
			});
		}


		// Implmentations.
		event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
		{
			add => this.propertyChangedHandlers += value;
			remove => this.propertyChangedHandlers -= value;
		}
	}
}
