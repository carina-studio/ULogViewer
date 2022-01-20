using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit entry of log property (<see cref="KeyValuePair{String, String}"/>).
	/// </summary>
	partial class StringLogPropertyMapEntryEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly TextBox mappedNameTextBox;
		readonly ComboBox nameComboBox;


		/// <summary>
		/// Initialize new <see cref="StringLogPropertyMapEntryEditorDialog"/> instance.
		/// </summary>
		public StringLogPropertyMapEntryEditorDialog()
		{
			InitializeComponent();
			this.mappedNameTextBox = this.FindControl<TextBox>(nameof(mappedNameTextBox)).AsNonNull();
			this.nameComboBox = this.FindControl<ComboBox>(nameof(nameComboBox)).AsNonNull();
		}


		/// <summary>
		/// Get or set entry to be edited.
		/// </summary>
		public KeyValuePair<string,string> Entry { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult((object?)new KeyValuePair<string, string>((string)this.nameComboBox.SelectedItem.AsNonNull(), this.mappedNameTextBox.Text.AsNonNull()));


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when property of editor changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == ComboBox.SelectedIndexProperty || e.Property == TextBox.TextProperty)
				this.InvalidateInput();
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var entry = this.Entry;
			this.nameComboBox.SelectedItem = entry.Key;
			if (this.nameComboBox.SelectedIndex < 0)
				this.nameComboBox.SelectedItem = nameof(Logs.Log.Message);
			this.mappedNameTextBox.Text = entry.Value;
			this.nameComboBox.Focus();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && this.nameComboBox.SelectedIndex >= 0 && !string.IsNullOrWhiteSpace(this.mappedNameTextBox.Text);
		}
	}
}
