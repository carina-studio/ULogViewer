using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Controls;
using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Main window.
	/// </summary>
	partial class MainWindow : BaseWindow
	{
		ViewModels.Session session;


		/// <summary>
		/// Initialize new <see cref="MainWindow"/> instance.
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();

			var sessionView = this.FindControl<SessionView>("sessionView").AsNonNull();
			session = new ViewModels.Session(App.Current);

			sessionView.DataContext = session;
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);

			this.SynchronizationContext.PostDelayed(() => session.SetLogProfileCommand.Execute(Logs.Profiles.LogProfiles.All[0]), 1000);
		}
	}
}
