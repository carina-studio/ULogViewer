using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit entry of log property (<see cref="KeyValuePair{String, String}"/>).
/// </summary>
class StringLogPropertyMapEntryEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly TextBox mappedNameTextBox;
	readonly ComboBox nameComboBox;


	/// <summary>
	/// Initialize new <see cref="StringLogPropertyMapEntryEditorDialog"/> instance.
	/// </summary>
	public StringLogPropertyMapEntryEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.mappedNameTextBox = this.Get<TextBox>(nameof(mappedNameTextBox));
		this.nameComboBox = this.Get<ComboBox>(nameof(nameComboBox));
	}


	/// <summary>
	/// Get or set entry to be edited.
	/// </summary>
	public KeyValuePair<string,string> Entry { get; init; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
		Task.FromResult((object?)new KeyValuePair<string, string>((string)this.nameComboBox.SelectedItem.AsNonNull(), this.mappedNameTextBox.Text.AsNonNull()));


	// Called when property of editor changed.
	void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == SelectingItemsControl.SelectedIndexProperty || e.Property == TextBox.TextProperty)
			this.InvalidateInput();
	}


	/// <inheritdoc/>
	protected override void OnEnterKeyClickedOnInputControl(Control control)
	{
		base.OnEnterKeyClickedOnInputControl(control);
		if (ReferenceEquals(control, this.mappedNameTextBox) && !string.IsNullOrWhiteSpace(this.mappedNameTextBox.Text))
			this.GenerateResultCommand.TryExecute();
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() => this.nameComboBox.Focus());
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		var entry = this.Entry;
		this.nameComboBox.SelectedItem = entry.Key;
		if (this.nameComboBox.SelectedIndex < 0)
			this.nameComboBox.SelectedItem = nameof(Logs.Log.Message);
		this.mappedNameTextBox.Text = entry.Value;
	}


	// Validate input.
	protected override bool OnValidateInput()
	{
		return base.OnValidateInput() && this.nameComboBox.SelectedIndex >= 0 && !string.IsNullOrWhiteSpace(this.mappedNameTextBox.Text);
	}
}