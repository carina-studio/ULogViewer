using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Editor of <see cref="LogPattern"/>.
/// </summary>
class LogPatternEditorDialog : InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly TextBox descriptionTextBox;
	readonly PatternEditor patternEditor;
	readonly ToggleSwitch repeatableSwitch;
	readonly ToggleSwitch skippableSwitch;


	/// <summary>
	/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
	/// </summary>
	public LogPatternEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.descriptionTextBox = this.Get<TextBox>(nameof(descriptionTextBox));
		this.patternEditor = this.Get<PatternEditor>(nameof(patternEditor)).Also(it =>
		{
			it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.repeatableSwitch = this.Get<ToggleSwitch>(nameof(repeatableSwitch));
		this.skippableSwitch = this.Get<ToggleSwitch>(nameof(skippableSwitch));
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		if (this.patternEditor.Pattern is null)
		{
			this.HintForInput(null, this.Get<Control>("patternItem"), this.patternEditor);
			return Task.FromResult<object?>(null);
		}
		var newLogPattern = new LogPattern(
			this.patternEditor.Pattern.AsNonNull(), 
			this.repeatableSwitch.IsChecked.GetValueOrDefault(), 
			this.skippableSwitch.IsChecked.GetValueOrDefault(),
			this.descriptionTextBox.Text?.Trim());
		return Task.FromResult<object?>(newLogPattern);
	}


	/// <summary>
	/// Get or set <see cref="LogPattern"/> to be edited.
	/// </summary>
	public LogPattern? LogPattern { get; init; }


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() =>
		{
			var presenter = this.TutorialPresenter;
			if (presenter is null || !this.patternEditor.ShowTutorialIfNeeded(presenter, this.patternEditor))
				this.patternEditor.Focus();
		});
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		this.LogPattern?.Let(it =>
		{
			this.descriptionTextBox.Text = it.Description?.Trim();
			this.patternEditor.Pattern = it.Regex;
			this.repeatableSwitch.IsChecked = it.IsRepeatable;
			this.skippableSwitch.IsChecked = it.IsSkippable;
		});
	}
}