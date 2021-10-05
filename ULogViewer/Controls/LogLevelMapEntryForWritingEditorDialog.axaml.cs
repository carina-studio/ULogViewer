using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="KeyValuePair{LogLevel, String}"/>.
	/// </summary>
	partial class LogLevelMapEntryForWritingEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ComboBox logLevelComboBox;
		readonly TextBox textBox;


		/// <summary>
		/// Initialize new <see cref="LogLevelMapEntryForWritingEditorDialog"/> instance.
		/// </summary>
		public LogLevelMapEntryForWritingEditorDialog()
		{
			InitializeComponent();
			this.logLevelComboBox = this.FindControl<ComboBox>("logLevelComboBox").AsNonNull();
			this.textBox = this.FindControl<TextBox>("textBox").AsNonNull();
		}


		/// <summary>
		/// Get or set entry to be edited.
		/// </summary>
		public KeyValuePair<LogLevel, string>? Entry { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult((object?)new KeyValuePair<LogLevel, string>((LogLevel)this.logLevelComboBox.SelectedItem.AsNonNull(), this.textBox.Text.AsNonNull()));


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var entry = this.Entry;
			if (entry == null)
				this.logLevelComboBox.SelectedItem = LogLevel.Undefined;
			else
			{
				this.logLevelComboBox.SelectedItem = entry.Value.Key;
				this.textBox.Text = entry.Value.Value;
			}
			this.logLevelComboBox.Focus();
			base.OnOpened(e);
		}


		// Called when property of text box changed.
		void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == TextBox.TextProperty)
				this.InvalidateInput();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && !string.IsNullOrEmpty(this.textBox.Text);
		}
	}
}
