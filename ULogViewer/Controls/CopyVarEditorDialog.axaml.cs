using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="CopyVariableAction"/>.
/// </summary>
partial class CopyVarEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly TextBox sourceVarTextBox;
	readonly TextBox targetVarTextBox;


	/// <summary>
	/// Initialize new <see cref="CopyVarEditorDialog"/> instance.
	/// </summary>
	public CopyVarEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.sourceVarTextBox = this.Get<TextBox>(nameof(sourceVarTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.targetVarTextBox = this.Get<TextBox>(nameof(targetVarTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
	}


	/// <summary>
	/// Get or set action to be edited.
	/// </summary>
	public CopyVariableAction? Action { get; set; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
		Task.FromResult<object?>(new CopyVariableAction(this.sourceVarTextBox.Text.AsNonNull().Trim(), this.targetVarTextBox.Text.AsNonNull().Trim()));


	// Dialog opened.
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var action = this.Action;
		if (action != null)
		{
			this.sourceVarTextBox.Text = action.SourceVariable.Trim();
			this.targetVarTextBox.Text = action.TargetVariable.Trim();
		}
		this.SynchronizationContext.Post(this.sourceVarTextBox.Focus);
	}


	// Validate input.
	protected override bool OnValidateInput() =>
		base.OnValidateInput() 
		&& !string.IsNullOrWhiteSpace(this.sourceVarTextBox.Text) 
		&& !string.IsNullOrWhiteSpace(this.targetVarTextBox.Text)
		&& this.sourceVarTextBox.Text != this.targetVarTextBox.Text;
}
