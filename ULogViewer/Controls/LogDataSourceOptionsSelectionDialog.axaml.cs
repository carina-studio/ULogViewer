using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to selection options of <see cref="LogDataSourceOptions"/>.
/// </summary>
partial class LogDataSourceOptionsSelectionDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly Avalonia.Controls.ListBox optionListBox;


	/// <summary>
	/// Initialize new <see cref="LogDataSourceOptionsSelectionDialog"/> instance.
	/// </summary>
	public LogDataSourceOptionsSelectionDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.optionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(optionListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (_, e) => this.SynchronizationContext.Post(() => this.GenerateResultCommand.TryExecute());
			it.SelectionChanged += (_, e) => this.InvalidateInput();
		});
	}


	/// <summary>
	/// Get or set available options for selection.
	/// </summary>
	public IEnumerable<string>? AvailableOptions { get; set; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var options = new HashSet<string>();
		foreach (var item in this.optionListBox.SelectedItems)
			options.Add((string)item.AsNonNull());
		return Task.FromResult<object?>(options);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var availableOptions = this.AvailableOptions?.ToArray() ?? new string[0];
		if (availableOptions.IsNotEmpty())
		{
			Array.Sort(availableOptions, (lhs, rhs) => string.Compare(lhs, rhs, true, CultureInfo.InvariantCulture));
			this.optionListBox.Items = availableOptions;
		}
		else
			this.SynchronizationContext.Post(this.Close);
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && this.optionListBox.SelectedItems.Count > 0;
}
