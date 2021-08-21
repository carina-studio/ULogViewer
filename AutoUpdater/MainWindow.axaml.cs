using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.ComponentModel;

namespace CarinaStudio.AutoUpdater
{
	// Main window.
	partial class MainWindow : Window
	{
		// Constructor.
		public MainWindow()
		{
			this.DataContext = App.Current;
			InitializeComponent();
		}


		// Cancel updating.
		void Cancel() => App.Current.Cancel();


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when closing.
		protected override void OnClosing(CancelEventArgs e)
		{
			if (!App.Current.IsCompleted)
				e.Cancel = true;
			base.OnClosing(e);
		}
	}
}
