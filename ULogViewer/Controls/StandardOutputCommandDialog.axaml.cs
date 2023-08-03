using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Threading;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit command for standard output.
/// </summary>
class StandardOutputCommandDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Static fields.
	static readonly AvaloniaProperty<bool?> UseTextShellProperty = AvaloniaProperty.Register<StandardOutputCommandDialog, bool?>(nameof(UseTextShell), null);


	// Fields.
	readonly TextBox commandTextBox;
	readonly ToggleSwitch useTextShellSwitch;


	/// <summary>
	/// Initialize new <see cref="StandardOutputCommandDialog"/> instance.
	/// </summary>
	public StandardOutputCommandDialog()
	{
		this.CommandSyntaxHighlightingDefinitionSet = Highlighting.TextShellCommandSyntaxHighlighting.CreateDefinitionSet(this.Application);
		AvaloniaXamlLoader.Load(this);
		this.commandTextBox = this.Get<TextBox>(nameof(commandTextBox));
		this.useTextShellSwitch = this.Get<ToggleSwitch>(nameof(useTextShellSwitch));
	}
	
	
	/// <summary>
	/// Get or set command.
	/// </summary>
	public string? Command { get; set; }


	/// <summary>
	/// Syntax highlighting definition set for command.
	/// </summary>
	public SyntaxHighlightingDefinitionSet CommandSyntaxHighlightingDefinitionSet { get; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		this.Command = this.commandTextBox.Text?.Trim();
		if (this.GetValue<bool?>(UseTextShellProperty).HasValue)
			this.SetValue(UseTextShellProperty, this.useTextShellSwitch.IsChecked.GetValueOrDefault());
		return Task.FromResult<object?>(this.Command);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() => this.commandTextBox.Focus());
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		this.commandTextBox.Text = this.Command;
		this.GetValue<bool?>(UseTextShellProperty)?.Let(it => this.useTextShellSwitch.IsChecked = it);
	}
	
	
	/// <summary>
	/// Show options dialog of default text shell.
	/// </summary>
	public void ShowDefaultTextShellOptions() =>
		this.Application.ShowApplicationOptionsDialogAsync(this, AppOptionsDialog.DefaultTextShellSection);


	/// <summary>
	/// Get or set whether text-shell should be used or not.
	/// </summary>
	public bool? UseTextShell
	{
		get => this.GetValue<bool?>(UseTextShellProperty);
		set => this.SetValue(UseTextShellProperty, value);
	}
}
