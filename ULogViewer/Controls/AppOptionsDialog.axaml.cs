using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.ViewModels;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog for application options.
	/// </summary>
	partial class AppOptionsDialog : BaseDialog
	{
		/// <summary>
		/// Initialize new <see cref="AppOptionsDialog"/> instance.
		/// </summary>
		public AppOptionsDialog()
		{
			this.DataContext = new AppOptions(App.Current);
			InitializeComponent();
		}

		
		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			(this.DataContext as AppOptions)?.Dispose();
			this.DataContext = null;
			base.OnClosed(e);
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;
	}
}
