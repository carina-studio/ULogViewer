using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="KeyValuePair{LogLevel, String}"/>.
	/// </summary>
	class LogLevelMapEntryForWritingEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
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
		public KeyValuePair<LogLevel, string>? Entry { get; init; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult((object?)new KeyValuePair<LogLevel, string>((LogLevel)this.logLevelComboBox.SelectedItem.AsNonNull(), this.textBox.Text.AsNonNull()));


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <inheritdoc/>
	protected override void OnEnterKeyClickedOnInputControl(Control control)
	{
		base.OnEnterKeyClickedOnInputControl(control);
		if (ReferenceEquals(control, this.textBox) && !string.IsNullOrWhiteSpace(this.textBox.Text))
			this.GenerateResultCommand.TryExecute();
	}


	/// <inheritdoc/>
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			this.SynchronizationContext.Post(() => this.logLevelComboBox.Focus());
		}


		/// <inheritdoc/>
		protected override void OnOpening(EventArgs e)
		{
			base.OnOpening(e);
			var entry = this.Entry;
			if (entry is null)
				this.logLevelComboBox.SelectedItem = LogLevel.Undefined;
			else
			{
				this.logLevelComboBox.SelectedItem = entry.Value.Key;
				this.textBox.Text = entry.Value.Value;
			}
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
			return base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.textBox.Text);
		}
	}
}
