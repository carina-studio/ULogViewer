using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="KeyValuePair{String, LogLevel}"/>.
	/// </summary>
	partial class LogLevelMapEntryEditorDialog : BaseDialog
	{
		// Fields.
		readonly ComboBox logLevelComboBox;
		readonly TextBox textBox;


		/// <summary>
		/// Initialize new <see cref="LogLevelMapEntryEditorDialog"/> instance.
		/// </summary>
		public LogLevelMapEntryEditorDialog()
		{
			InitializeComponent();
			this.logLevelComboBox = this.FindControl<ComboBox>("logLevelComboBox").AsNonNull();
			this.textBox = this.FindControl<TextBox>("textBox").AsNonNull();
		}


		/// <summary>
		/// Get or set entry to be edited.
		/// </summary>
		public KeyValuePair<string,LogLevel>? Entry { get; set; }


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Generate result.
		protected override object? OnGenerateResult() => new KeyValuePair<string, LogLevel>(this.textBox.Text.AsNonNull(), (LogLevel)this.logLevelComboBox.SelectedItem.AsNonNull());


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var entry = this.Entry;
			if (entry == null)
				this.logLevelComboBox.SelectedItem = LogLevel.Info;
			else
			{
				this.logLevelComboBox.SelectedItem = entry.Value.Value;
				this.textBox.Text = entry.Value.Key;
			}
			this.textBox.Focus();
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
