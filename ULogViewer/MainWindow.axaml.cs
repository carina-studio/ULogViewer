using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Main window.
	/// </summary>
	partial class MainWindow : Window
	{
		/// <summary>
		/// Initialize new <see cref="MainWindow"/> instance.
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
	}
}
