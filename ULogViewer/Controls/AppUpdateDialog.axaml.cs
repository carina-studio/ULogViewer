using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
using System;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog for application update.
	/// </summary>
	partial class AppUpdateDialog : BaseDialog
	{
		/// <summary>
		/// Key of persistent state of the latest version notified user.
		/// </summary>
		public static readonly SettingKey<string> LatestNotifiedVersionKey = new SettingKey<string>("LatestNotifiedVersion", "");


		// Fields.
		bool isClosingRequested;


		/// <summary>
		/// Initialize new <see cref="AppUpdateDialog"/> instance.
		/// </summary>
		public AppUpdateDialog()
		{
			this.DataContext = new AppUpdater(this.Application).Also(it =>
			{
				it.ErrorMessageGenerated += (_, e) =>
				{
					new MessageDialog()
					{
						Icon = MessageDialogIcon.Error,
						Message = e.Message,
					}.ShowDialog(this);
				};
				it.PropertyChanged += (_, e) =>
				{
					if (e.PropertyName == nameof(AppUpdater.IsPreparingForUpdate)
						&& !it.IsPreparingForUpdate
						&& this.isClosingRequested)
					{
						this.Close();
					}
					else if (e.PropertyName == nameof(AppUpdater.UpdateVersion))
						this.Application.PersistentState.SetValue<string>(LatestNotifiedVersionKey, it.UpdateVersion?.ToString() ?? "");
				};
				this.Application.PersistentState.SetValue<string>(LatestNotifiedVersionKey, it.UpdateVersion?.ToString() ?? "");
			});
			InitializeComponent();
		}


		/// <summary>
		/// Get or set whether performing update checking when opening dialog or not.
		/// </summary>
		public bool CheckForUpdateWhenOpening { get; set; }


		// Download update package.
		void DownloadUpdatePackage()
		{
			if (this.DataContext is AppUpdater updater && updater.UpdatePackageUri != null)
			{
				this.OpenLink(updater.UpdatePackageUri);
				this.Close();
			}
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			(this.DataContext as IDisposable)?.Dispose();
			base.OnClosed(e);
		}


		// Called when closing.
		protected override void OnClosing(CancelEventArgs e)
		{
			if (this.DataContext is AppUpdater updater)
			{
				if (updater.IsPreparingForUpdate)
				{
					e.Cancel = true;
					this.isClosingRequested = true;
					updater.CancelUpdatingCommand.TryExecute();
				}
			}
			base.OnClosing(e);
		}


		// Called when pointer released on description text.
		void OnLinkDescriptionPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton != MouseButton.Left)
				return;
			if (sender is Control control && control.Tag is Uri uri)
				this.OpenLink(uri);
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			if (this.CheckForUpdateWhenOpening && this.DataContext is AppUpdater updater)
				updater.CheckForUpdateCommand.TryExecute();
			base.OnOpened(e);
		}
	}
}
