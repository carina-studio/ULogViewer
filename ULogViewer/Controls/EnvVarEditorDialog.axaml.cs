using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit environment variable.
/// </summary>
class EnvVarEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly TextBox nameTextBox;
	readonly TextBox valueTextBox;


	/// <summary>
	/// Initialize new <see cref="EnvVarEditorDialog"/> instance.
	/// </summary>
	public EnvVarEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.TextChanged += (_, _) => this.InvalidateInput();
		});
		this.valueTextBox = this.Get<TextBox>(nameof(valueTextBox));
	}
	
	
	/// <summary>
	/// Get or set environment variable to be edited.
	/// </summary>
	public Tuple<string, string>? EnvironmentVariable { get; set; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
		Task.FromResult<object?>(new Tuple<string, string>(this.nameTextBox.Text.AsNonNull().Trim(), this.valueTextBox.Text?.Trim() ?? ""));


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() =>
		{
			if (string.IsNullOrEmpty(this.nameTextBox.Text))
				this.nameTextBox.Focus();
			else
			{
				this.valueTextBox.Focus();
				this.valueTextBox.SelectAll();
			}
		});
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		this.EnvironmentVariable?.Let(it =>
		{
			this.nameTextBox.Text = it.Item1.Trim();
			this.valueTextBox.Text = it.Item2.Trim();
		});
	}


	// Validate input.
	protected override bool OnValidateInput() =>
		base.OnValidateInput()
		&& !string.IsNullOrWhiteSpace(this.nameTextBox.Text);
}
