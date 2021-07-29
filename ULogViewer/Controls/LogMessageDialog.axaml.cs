using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using ReactiveUI;
using System;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to show message of log.
	/// </summary>
	partial class LogMessageDialog : BaseDialog
	{
		// Static fields.
		static readonly AvaloniaProperty<string> LogMessageDisplayNameProperty = AvaloniaProperty.Register<LogMessageDialog, string>(nameof(LogMessageDisplayName), "");


		// Fields.
		readonly TextBox messageTextBox;


		/// <summary>
		/// Initialize new <see cref="LogMessageDialog"/> instance.
		/// </summary>
		public LogMessageDialog()
		{
			this.SetTextWrappingCommand = ReactiveCommand.Create<bool>(this.SetTextWrapping);
			InitializeComponent();
			this.messageTextBox = this.FindControl<TextBox>("messageTextBox").AsNonNull().Also(it =>
			{
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == TextBox.BoundsProperty && this.messageTextBox != null)
						this.messageTextBox.Margin = new Thickness(0); // [Workaround] Relayout completed, restore to correct margin.
				};
			});
			this.SetValue<string>(LogMessageDisplayNameProperty, LogPropertyNameConverter.Default.Convert(nameof(Log.Message)));
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set log to show message.
		/// </summary>
		public Log? Log { get; set; }


		/// <summary>
		/// Get or set display name of <see cref="Log.Message"/>.
		/// </summary>
		public string LogMessageDisplayName
		{
			get => this.GetValue<string>(LogMessageDisplayNameProperty);
			set => this.SetValue<string>(LogMessageDisplayNameProperty, value);
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			this.messageTextBox.Text = this.Log?.Message;
			this.messageTextBox.Focus();
			base.OnOpened(e);
		}


		// set text wrapping.
		void SetTextWrapping(bool wrap)
		{
			if (this.messageTextBox == null)
				return;
			this.messageTextBox.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
			this.messageTextBox.Margin = new Thickness(1); // [Workaround] Force relayout to apply text wrapping.
		}


		// Command to set text wrapping.
		ICommand SetTextWrappingCommand { get; }
	}
}
