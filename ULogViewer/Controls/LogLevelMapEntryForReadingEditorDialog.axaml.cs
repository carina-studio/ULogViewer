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
	/// Dialog to edit <see cref="KeyValuePair{String, LogLevel}"/>.
	/// </summary>
	partial class LogLevelMapEntryForReadingEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ComboBox logLevelComboBox;
		readonly TextBox textBox;


		/// <summary>
		/// Initialize new <see cref="LogLevelMapEntryForReadingEditorDialog"/> instance.
		/// </summary>
		public LogLevelMapEntryForReadingEditorDialog()
		{
			InitializeComponent();
			this.logLevelComboBox = this.FindControl<ComboBox>("logLevelComboBox").AsNonNull();
			this.textBox = this.FindControl<TextBox>("textBox").AsNonNull();
		}


		/// <summary>
		/// Get or set entry to be edited.
		/// </summary>
		public KeyValuePair<string,LogLevel>? Entry { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult((object?)new KeyValuePair<string, LogLevel>(this.textBox.Text.AsNonNull(), (LogLevel)this.logLevelComboBox.SelectedItem.AsNonNull()));


        // Initialize.
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


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
