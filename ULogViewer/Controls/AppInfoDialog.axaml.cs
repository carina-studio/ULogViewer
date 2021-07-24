using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Reflection;

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


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


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
