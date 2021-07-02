using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.ViewModels;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// View of <see cref="Session"/>.
	/// </summary>
	partial class SessionView : UserControl
	{
		/// <summary>
		/// Initialize new <see cref="SessionView"/> instance.
		/// </summary>
		public SessionView()
		{
			// initialize
			this.InitializeComponent();
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
	}
}
