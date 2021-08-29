using CarinaStudio.AutoUpdate;
using CarinaStudio.AutoUpdate.Installers;
using CarinaStudio.AutoUpdate.Resolvers;
using CarinaStudio.Configuration;
using CarinaStudio.IO;
using CarinaStudio.Net;
using CarinaStudio.Threading;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using Mono.Unix;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Application updater.
	/// </summary>
	class AppUpdater : ViewModel
	{
		// Static fields.
		static readonly Regex AutoUpdaterDirNameRegex = new Regex("^AutoUpdater\\-(?<Version>[\\d\\.]+)$", RegexOptions.IgnoreCase);
		static readonly ObservableProperty<bool> IsCheckingForUpdateProperty = ObservableProperty.Register<AppUpdater, bool>(nameof(IsCheckingForUpdate));
		static readonly ObservableProperty<bool> IsLatestVersionProperty = ObservableProperty.Register<AppUpdater, bool>(nameof(IsLatestVersion));
		static readonly ObservableProperty<bool> IsPreparingForUpdateProperty = ObservableProperty.Register<AppUpdater, bool>(nameof(IsPreparingForUpdate));
		static readonly ObservableProperty<bool> IsUpdatePreparationProgressAvailableProperty = ObservableProperty.Register<AppUpdater, bool>(nameof(IsUpdatePreparationProgressAvailable));
		static readonly ObservableProperty<Uri?> ReleasePageUriProperty = ObservableProperty.Register<AppUpdater, Uri?>(nameof(ReleasePageUri));
		static readonly ObservableProperty<Uri?> UpdatePackageUriProperty = ObservableProperty.Register<AppUpdater, Uri?>(nameof(UpdatePackageUri));
		static readonly ObservableProperty<string?> UpdatePreparationMessageProperty = ObservableProperty.Register<AppUpdater, string?>(nameof(UpdatePreparationMessage));
		static readonly ObservableProperty<double> UpdatePreparationProgressPercentageProperty = ObservableProperty.Register<AppUpdater, double>(nameof(UpdatePreparationProgressPercentage));
		static readonly ObservableProperty<Version?> UpdateVersionProperty = ObservableProperty.Register<AppUpdater, Version?>(nameof(UpdateVersion));


		// Fields.
		Updater? auUpdater;
		readonly MutableObservableBoolean canCheckForUpdate = new MutableObservableBoolean(true);
		readonly MutableObservableBoolean canStartUpdating = new MutableObservableBoolean();
		CancellationTokenSource? updatePreparationCancellationTokenSource;


		/// <summary>
		/// Initialize new <see cref="AppUpdater"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public AppUpdater(IApplication app) : base(app)
		{
			this.CancelUpdatingCommand = new Command(this.CancelUpdating, this.GetValueAsObservable(IsPreparingForUpdateProperty));
			this.CheckForUpdateCommand = new Command(this.CheckForUpdate, this.canCheckForUpdate);
			this.StartUpdatingCommand = new Command(this.StartUpdating, this.canStartUpdating);
			this.ReportUpdateInfo();
		}


		// Cancel auto updating.
		void CancelUpdating()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsPreparingForUpdate)
				return;

			this.Logger.LogWarning("Cancel auto updating");

			// cancel
			this.updatePreparationCancellationTokenSource?.Cancel();
			this.auUpdater?.Cancel();
		}


		/// <summary>
		/// Command to cancel auto updating.
		/// </summary>
		public ICommand CancelUpdatingCommand { get; }


		// Check for update.
		async void CheckForUpdate()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (this.IsCheckingForUpdate)
				return;

			// check for update
			this.canCheckForUpdate.Update(false);
			this.SetValue(IsCheckingForUpdateProperty, true);
			await ((App)this.Application).CheckUpdateInfo();
			if (!this.IsDisposed)
			{
				this.SetValue(IsCheckingForUpdateProperty, false);
				this.canCheckForUpdate.Update(true);
			}
		}


		/// <summary>
		/// Command to check for application update.
		/// </summary>
		public ICommand CheckForUpdateCommand { get; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// cancel updating
			this.auUpdater?.Dispose();

			// call base
			base.Dispose(disposing);
		}


		/// <summary>
		/// Raised when error message generated.
		/// </summary>
		public event EventHandler<MessageEventArgs>? ErrorMessageGenerated;


		/// <summary>
		/// Check whether auto update is supported or not.
		/// </summary>
		public bool IsAutoUpdateSupported { get; } = Global.Run(() =>
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				|| RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
		});


		/// <summary>
		/// Check whether application update checking is on-going or not.
		/// </summary>
		public bool IsCheckingForUpdate { get => this.GetValue(IsCheckingForUpdateProperty); }


		/// <summary>
		/// Check whether application version is the latest or not.
		/// </summary>
		public bool IsLatestVersion { get => this.GetValue(IsLatestVersionProperty); }


		/// <summary>
		/// Check whether update preparation is on-going or not.
		/// </summary>
		public bool IsPreparingForUpdate { get => this.GetValue(IsPreparingForUpdateProperty); }


		/// <summary>
		/// Check whether value of <see cref="UpdatePreparationProgressPercentage"/> is available or not.
		/// </summary>
		public bool IsUpdatePreparationProgressAvailable { get => this.GetValue(IsUpdatePreparationProgressAvailableProperty); }


		// Called when property of application changed.
		protected override void OnApplicationPropertyChanged(PropertyChangedEventArgs e)
		{
			base.OnApplicationPropertyChanged(e);
			if (e.PropertyName == nameof(IApplication.UpdateInfo))
				this.ReportUpdateInfo();
		}


		// Called when property of updater of AutoUpdater changed.
		void OnAuUpdaterPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not Updater updater)
				return;
			switch (e.PropertyName)
			{
				case nameof(Updater.Progress):
					if (this.IsPreparingForUpdate)
						this.SetValue(UpdatePreparationProgressPercentageProperty, updater.Progress * 100);
					break;
				case nameof(Updater.State):
					this.RefreshMessages();
					switch (updater.State)
					{
						case UpdaterState.Cancelled:
						case UpdaterState.Failed:
						case UpdaterState.Succeeded:
							this.OnUpdatePreparationCompleted(updater.State, updater.ApplicationDirectoryPath.AsNonNull());
							break;
					}
					break;
			}
		}


		// Called when property changed.
		protected override void OnPropertyChanged(ObservableProperty property, object? oldValue, object? newValue)
		{
			base.OnPropertyChanged(property, oldValue, newValue);
			if (property == UpdatePreparationProgressPercentageProperty)
				this.SetValue(IsUpdatePreparationProgressAvailableProperty, double.IsFinite(this.UpdatePreparationProgressPercentage));
		}


		// Called when update preparation completed.
		async void OnUpdatePreparationCompleted(UpdaterState state, string autoUpdaterDirectory)
		{
			// release updater
			this.auUpdater?.Let(it =>
			{
				it.PropertyChanged -= this.OnAuUpdaterPropertyChanged;
				it.Dispose();
				this.auUpdater = null;
			});

			// update state
			this.SetValue(IsPreparingForUpdateProperty, false);
			this.RefreshMessages();

			// check state
			switch (state)
			{
				case UpdaterState.Succeeded:
					break;
				case UpdaterState.Cancelled:
					this.Logger.LogWarning("Update preparation was cancelled");
					return;
				default:
					this.Logger.LogError("Update preparation was failed");
					this.ErrorMessageGenerated?.Invoke(this, new MessageEventArgs(this.Application.GetStringNonNull("AppUpdater.FailedToPrepareForUpdate")));
					return;
			}

			// prepare auto updater
			var autoUpdaterPath = autoUpdaterDirectory.Let(it =>
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					return Path.Combine(it, "AutoUpdater.Avalonia.exe");
				return Path.Combine(it, "AutoUpdater.Avalonia");
			});
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				try
				{
					await Task.Run(() => new UnixFileInfo(autoUpdaterPath).FileAccessPermissions |= (FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute));
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, $"Unable to mark '{autoUpdaterPath}' as executable");
				}
			}

			// start auto updater
			try
			{
				var currentProcess = Process.GetCurrentProcess();
				var mainModule = currentProcess.MainModule;
				if (mainModule == null)
				{
					this.Logger.LogError("Unable to get information of current process");
					this.ErrorMessageGenerated?.Invoke(this, new MessageEventArgs(this.Application.GetStringNonNull("AppUpdater.FailedToPrepareForUpdate")));
					return;
				}
				var useDarkMode = this.Settings.GetValueOrDefault(ULogViewer.Settings.ThemeMode) == ThemeMode.Dark;
				this.Logger.LogWarning("Start auto updater");
				using var process = Process.Start(new ProcessStartInfo()
				{
					Arguments = $"-directory \"{Path.GetDirectoryName(mainModule.FileName)}\" -executable \"{mainModule.FileName}\" -package-manifest {Uris.AppPackageManifest} -wait-for-process {currentProcess.Id} {(useDarkMode ? "-dark-mode" : "")}",
					FileName = autoUpdaterPath,
				});
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to start auto updater");
				this.ErrorMessageGenerated?.Invoke(this, new MessageEventArgs(this.Application.GetStringNonNull("AppUpdater.FailedToStartAutoUpdater")));
				return;
			}

			// shutdown
			this.Logger.LogWarning("Shutdown for updating");
			//((App)this.Application).Shutdown();
		}


		// Refresh messages according to current state.
		void RefreshMessages()
		{
			if (this.IsDisposed)
				return;
			if (this.IsPreparingForUpdate)
			{
				if (this.auUpdater?.State == UpdaterState.DownloadingPackage)
				{
					var downloadedSizeString = this.auUpdater.DownloadedPackageSize.ToFileSizeString();
					var packageSize = this.auUpdater.PackageSize.GetValueOrDefault();
					if (packageSize > 0)
						this.SetValue(UpdatePreparationMessageProperty, this.Application.GetFormattedString("AppUpdater.DownloadingAutoUpdater", $"{downloadedSizeString} / {packageSize.ToFileSizeString()}"));
					else
						this.SetValue(UpdatePreparationMessageProperty, this.Application.GetFormattedString("AppUpdater.DownloadingAutoUpdater", downloadedSizeString));
				}
				else
					this.SetValue(UpdatePreparationMessageProperty, this.Application.GetString("AppUpdater.PreparingForUpdate"));
			}
			else
				this.SetValue(UpdatePreparationMessageProperty, null);
		}


		/// <summary>
		/// Get URI of update releasing page.
		/// </summary>
		public Uri? ReleasePageUri { get => this.GetValue(ReleasePageUriProperty); }


		// Report current application update info.
		void ReportUpdateInfo()
		{
			var updateInfo = ((App)this.Application).UpdateInfo;
			if (updateInfo == null)
			{
				this.canStartUpdating.Update(false);
				this.SetValue(IsLatestVersionProperty, true);
				this.SetValue(ReleasePageUriProperty, null);
				this.SetValue(UpdatePackageUriProperty, null);
				this.SetValue(UpdateVersionProperty, null);
			}
			else
			{
				this.SetValue(IsLatestVersionProperty, false);
				this.SetValue(ReleasePageUriProperty, updateInfo.ReleasePageUri);
				this.SetValue(UpdatePackageUriProperty, updateInfo.PackageUri);
				this.SetValue(UpdateVersionProperty, updateInfo.Version);
				this.canStartUpdating.Update(IsAutoUpdateSupported && !this.IsPreparingForUpdate);
			}
		}


		// Start auto updating.
		async void StartUpdating()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canStartUpdating.Value
				|| !this.IsAutoUpdateSupported
				|| this.IsPreparingForUpdate)
			{
				return;
			}

			// update state
			this.canStartUpdating.Update(false);
			this.SetValue(IsPreparingForUpdateProperty, true);

			// update message
			this.RefreshMessages();

			// resolve info of auto updater
			this.updatePreparationCancellationTokenSource = new CancellationTokenSource();
			var auPackageResolver = new JsonPackageResolver() { Source = new WebRequestStreamProvider(Uris.AutoUpdaterPackageManifest) };
			try
			{
				await auPackageResolver.StartAndWaitAsync(this.updatePreparationCancellationTokenSource.Token);
			}
			catch (TaskCanceledException)
			{ }
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Failed to resolve package info of auto updater");
			}
			if (this.IsDisposed)
				return;
			if (this.updatePreparationCancellationTokenSource.IsCancellationRequested)
			{
				this.OnUpdatePreparationCompleted(UpdaterState.Cancelled, "");
				return;
			}

			// check current auto updater version
			var autoUpdaterDirectory = await Task.Run(() =>
			{
				// get current version
				var currentVersion = (Version?)null;
				var selectedDirectory = (string?)null;
				try
				{
					foreach (var path in Directory.EnumerateDirectories(this.Application.RootPrivateDirectoryPath))
					{
						var match = AutoUpdaterDirNameRegex.Match(Path.GetFileName(path));
						if (match.Success && Version.TryParse(match.Groups["Version"].Value, out var version))
						{
							if (version > currentVersion)
							{
								currentVersion = version;
								selectedDirectory = path;
							}
						}
					}
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Failed to check current version of auto updater");
					currentVersion = null;
					selectedDirectory = null;
				}

				// delete current auto updater if needed
				var needToUpdate = (currentVersion != auPackageResolver.PackageVersion);
				if (needToUpdate)
					selectedDirectory = null;
				try
				{
					foreach (var path in Directory.EnumerateDirectories(this.Application.RootPrivateDirectoryPath))
					{
						if (AutoUpdaterDirNameRegex.IsMatch(Path.GetFileName(path)) && !PathEqualityComparer.Default.Equals(path, selectedDirectory))
						{
							try
							{
								this.Logger.LogDebug($"Delete auto updater '{path}'");
								Directory.Delete(path, true);
							}
							catch (Exception ex)
							{
								this.Logger.LogError(ex, $"Failed to delete auto updater '{path}'");
							}
						}
					}
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Error occurred while deleting auto updater");
				}

				// complete
				return selectedDirectory;
			});
			if (this.IsDisposed)
				return;
			if (this.updatePreparationCancellationTokenSource.IsCancellationRequested)
			{
				this.OnUpdatePreparationCompleted(UpdaterState.Cancelled, "");
				return;
			}

			// download auto updater
			if (autoUpdaterDirectory == null)
			{
				this.auUpdater = new Updater()
				{
					ApplicationDirectoryPath = Path.Combine(this.Application.RootPrivateDirectoryPath, $"AutoUpdater-{auPackageResolver.PackageVersion}"),
					PackageInstaller = new ZipPackageInstaller(),
					PackageResolver = new JsonPackageResolver() { Source = new WebRequestStreamProvider(Uris.AutoUpdaterPackageManifest) },
				};
				this.Logger.LogWarning($"Start downloading auto updater to '{this.auUpdater.ApplicationDirectoryPath}'");
				this.auUpdater.PropertyChanged += this.OnAuUpdaterPropertyChanged;
				if (!this.auUpdater.Start())
				{
					this.Logger.LogError("Failed to start downloading auto updater");
					this.OnUpdatePreparationCompleted(UpdaterState.Failed, "");
				}
			}
			else
			{
				this.Logger.LogDebug($"Use current auto updater '{autoUpdaterDirectory}' directly");
				this.OnUpdatePreparationCompleted(UpdaterState.Succeeded, autoUpdaterDirectory);
			}
		}


		/// <summary>
		/// Command to start updating application.
		/// </summary>
		public ICommand StartUpdatingCommand { get; }


		/// <summary>
		/// Get URI of update package.
		/// </summary>
		public Uri? UpdatePackageUri { get => this.GetValue(UpdatePackageUriProperty); }


		/// <summary>
		/// Get message to describe the status of update preparation.
		/// </summary>
		public string? UpdatePreparationMessage { get => this.GetValue(UpdatePreparationMessageProperty); }


		/// <summary>
		/// Get current progress percentage of update preparation.
		/// </summary>
		public double UpdatePreparationProgressPercentage { get => this.GetValue(UpdatePreparationProgressPercentageProperty); }


		/// <summary>
		/// Get new version which application can be updated to.
		/// </summary>
		public Version? UpdateVersion { get => this.GetValue(UpdateVersionProperty); }
	}
}
