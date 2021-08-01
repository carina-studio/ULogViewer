using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Splash window when launching application.
	/// </summary>
	partial class SplashWindow : Window
	{
		// Static fields.
		static readonly AvaloniaProperty<string> MessageProperty = AvaloniaProperty.Register<SplashWindow, string>(nameof(Message), " ", coerce: ((_, it) => string.IsNullOrEmpty(it) ? " " : it));


		/// <summary>
		/// Initialize new <see cref="SplashWindow"/>.
		/// </summary>
		public SplashWindow()
		{
			this.Version = $"v{App.Current.Assembly.GetName().Version}";
			InitializeComponent();
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set message to show.
		/// </summary>
		public string Message
		{
			get => this.GetValue<string>(MessageProperty);
			set => this.SetValue<string>(MessageProperty, value);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			// call base
			base.OnOpened(e);

			// move to center of screen
			var screen = this.Screens.ScreenFromVisual(this);
			var screenBounds = screen.Bounds;
			var pixelDensity = screen.PixelDensity;
			var width = this.Width * pixelDensity;
			var height = this.Height * pixelDensity;
			this.Position = new PixelPoint((int)((screenBounds.Width - width) / 2), (int)((screenBounds.Height - height) / 2));

			// show content
			((Control)(this.Content)).Opacity = 1;
		}


		// String represents version.
		string Version { get; }
	}
}
