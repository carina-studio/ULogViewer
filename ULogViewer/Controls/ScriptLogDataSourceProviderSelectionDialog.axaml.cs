using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to select <see cref="ScriptLogDataSourceProvider"/>.
/// </summary>
partial class ScriptLogDataSourceProviderSelectionDialog : CarinaStudio.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly Avalonia.Controls.ListBox providerListBox;


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProviderSelectionDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProviderSelectionDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.providerListBox = this.Get<AppSuite.Controls.ListBox>(nameof(providerListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (_, _) => this.GenerateResultCommand.TryExecute();
			it.SelectionChanged += (_, _) => this.InvalidateInput();
		});
	}


	/// <inheritdoc/>
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
		Task.FromResult<object?>(this.providerListBox.SelectedItem as ScriptLogDataSourceProvider);


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(this.providerListBox.Focus);
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && this.providerListBox.SelectedItem != null;
}
