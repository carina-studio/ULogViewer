using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to select existing log analysis script set.
/// </summary>
partial class LogAnalysisScriptSetSelectionDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly CarinaStudio.AppSuite.Controls.ListBox scriptSetListBox;


	/// <summary>
	/// Initialize new <see cref="LogAnalysisScriptSetSelectionDialog"/> instance.
	/// </summary>
	public LogAnalysisScriptSetSelectionDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.scriptSetListBox = this.Get<CarinaStudio.AppSuite.Controls.ListBox>(nameof(scriptSetListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (_, _) => this.GenerateResultCommand.TryExecute();
			it.SelectionChanged += (_, _) => this.InvalidateInput();
		});
	}


	/// <inheritdoc/>
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
		Task.FromResult<object?>(this.scriptSetListBox.SelectedItem as LogAnalysisScriptSet);


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(_ => this.scriptSetListBox.Focus(), null);
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && this.scriptSetListBox.SelectedItem != null;
}