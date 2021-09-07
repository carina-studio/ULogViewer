using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.IO;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Application info dialog.
	/// </summary>
	partial class AppInfoDialog : BaseDialog
	{
		/// <summary>
		/// Initialize new <see cref="AppInfoDialog"/> instance.
		/// </summary>
		public AppInfoDialog()
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version.AsNonNull();
			this.VersionString = this.Application.GetFormattedString("AppInfoDialog.Version", version);
			InitializeComponent();
		}


		// Export application logs to file.
		async void ExportAppLogs()
		{
			// select file
			var fileName = await new SaveFileDialog().Also(it =>
			{
				var dateTime = DateTime.Now;
				it.Filters.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("txt");
					filter.Name = this.Application.GetString("FileFormat.Text");
				}));
				it.InitialFileName = $"ULogViewer-{dateTime.ToString("yyyyMMdd-HHmmss")}.txt";
			}).ShowAsync(this);
			if (fileName == null)
				return;

			// export
			var succeeded = await Task.Run(() =>
			{
				try
				{
					var logFilePath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName).AsNonNull(), "Log", "log.txt");
					if (PathEqualityComparer.Default.Equals(logFilePath, fileName))
						return false;
					System.IO.File.Copy(logFilePath, fileName, true);
					return true;
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Failed to export application logs");
					return false;
				}
			});

			// show result
			if (!this.IsOpened)
				return;
			if (succeeded)
			{
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Information,
					Message = this.Application.GetString("AppInfoDialog.SucceededToExportAppLogs"),
				}.ShowDialog(this);
			}
			else
			{
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = this.Application.GetString("AppInfoDialog.FailedToExportAppLogs"),
				}.ShowDialog(this);
			}
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when pointer released on text of export app logs.
		void OnExportAppLogsTextBlockPointerReleased(object? sender, PointerReleasedEventArgs e) =>
			this.ExportAppLogs();


		// Generate result.
		protected override object? OnGenerateResult() => null;


		// Called when pointer released on link text.
		void OnLinkTextPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton == MouseButton.Left && (sender as Control)?.Tag is string uri)
				this.OpenLink(uri);
		}


		// String represent version.
		string VersionString { get; }
	}
}
