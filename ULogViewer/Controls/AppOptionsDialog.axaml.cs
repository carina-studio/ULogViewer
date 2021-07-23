using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog for application options.
	/// </summary>
	partial class AppOptionsDialog : BaseDialog
	{
		// Fields.
		readonly ScheduledAction refreshDataContextAction;


		/// <summary>
		/// Initialize new <see cref="AppOptionsDialog"/> instance.
		/// </summary>
		public AppOptionsDialog()
		{
			this.DataContext = new AppOptions(App.Current);
			InitializeComponent();
			App.Current.StringsUpdated += this.OnApplicationStringsUpdated;
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.refreshDataContextAction = new ScheduledAction(() =>
			{
				var dataContext = this.DataContext;
				this.DataContext = null;
				this.DataContext = dataContext;
			});
		}

		
		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when strings updated.
		void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			this.refreshDataContextAction.Schedule();
		}


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			App.Current.StringsUpdated -= this.OnApplicationStringsUpdated;
			this.Settings.SettingChanged -= this.OnSettingChanged;
			this.refreshDataContextAction.Cancel();
			(this.DataContext as AppOptions)?.Dispose();
			this.DataContext = null;
			base.OnClosed(e);
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == Settings.ThemeMode)
				this.refreshDataContextAction.Reschedule(500);
		}
	}
}
