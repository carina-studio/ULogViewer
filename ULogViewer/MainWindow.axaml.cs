using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.ViewModels;
using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Main window.
	/// </summary>
	partial class MainWindow : BaseWindow
	{
		/// <summary>
		/// Initialize new <see cref="MainWindow"/> instance.
		/// </summary>
		public MainWindow()
		{
			// initialize.
			InitializeComponent();
		}


		// Attach to workspace.
		void AttachToWorkspace(Workspace workspace)
		{
			var session = workspace.CreateSession();
			var sessionView = this.FindControl<SessionView>("sessionView").AsNonNull();
			sessionView.DataContext = session;
		}


		// Detach from workspace.
		void DetachFronWorkspace(Workspace workspace)
		{

		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == DataContextProperty)
			{
				(change.OldValue.Value as Workspace)?.Let(it => this.DetachFronWorkspace(it));
				(change.NewValue.Value as Workspace)?.Let(it => this.AttachToWorkspace(it));
			}
		}
	}
}
