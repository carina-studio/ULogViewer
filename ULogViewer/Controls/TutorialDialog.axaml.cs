using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Tutorial dialog.
	/// </summary>
	partial class TutorialDialog : BaseDialog
	{
		/// <summary>
		/// Property of <see cref="Message"/>.
		/// </summary>
		public static readonly AvaloniaProperty<string?> MessageProperty = AvaloniaProperty.Register<TutorialDialog, string?>(nameof(Message));
		/// <summary>
		/// Property of <see cref="Screenshot"/>.
		/// </summary>
		public static readonly AvaloniaProperty<IImage?> ScreenshotProperty = AvaloniaProperty.Register<TutorialDialog, IImage?>(nameof(Screenshot));


		/// <summary>
		/// Initialize new <see cref="TutorialDialog"/> instance.
		/// </summary>
		public TutorialDialog()
		{
			InitializeComponent();
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set message of tutorial.
		/// </summary>
		public string? Message
		{
			get => this.GetValue<string?>(MessageProperty);
			set => this.SetValue<string?>(MessageProperty, value);
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;


		/// <summary>
		/// Get or set screenshot of tutorial.
		/// </summary>
		public IImage? Screenshot
		{
			get => this.GetValue<IImage?>(ScreenshotProperty);
			set => this.SetValue<IImage?>(ScreenshotProperty, value);
		}
	}
}
