using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Threading;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="KeyLogAnalysisRuleSet.Rule"/>.
/// </summary>
partial class KeyLogAnalysisRuleEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly ComboBox levelComboBox;
	readonly StringInterpolationFormatTextBox messageTextBox;
	readonly PatternEditor patternEditor;
	readonly ComboBox resultTypeComboBox;


	/// <summary>
	/// Initialize new <see cref="KeyLogAnalysisRuleEditorDialog"/> instance.
	/// </summary>
	public KeyLogAnalysisRuleEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.levelComboBox = this.Get<ComboBox>(nameof(levelComboBox));
		this.messageTextBox = this.Get<StringInterpolationFormatTextBox>(nameof(messageTextBox)).Also(it =>
		{
			foreach (var propertyName in Log.PropertyNames)
			{
				it.PredefinedVariables.Add(new StringInterpolationVariable().Also(variable =>
				{
					variable.Bind(StringInterpolationVariable.DisplayNameProperty, new Binding() 
					{
						Converter = Converters.LogPropertyNameConverter.Default,
						Path = nameof(StringInterpolationVariable.Name),
						Source = variable,
					});
					variable.Name = propertyName;
				}));
			}
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.patternEditor = this.Get<PatternEditor>(nameof(patternEditor)).Also(it =>
		{
			it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.resultTypeComboBox = this.Get<ComboBox>(nameof(resultTypeComboBox));
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var rule = new KeyLogAnalysisRuleSet.Rule(this.patternEditor.Pattern.AsNonNull(), 
			(Logs.LogLevel)this.levelComboBox.SelectedItem!,
			(DisplayableLogAnalysisResultType)this.resultTypeComboBox.SelectedItem!, 
			this.messageTextBox.Text.AsNonNull());
		if (rule.Equals(this.Rule))
			return Task.FromResult<object?>(this.Rule);
		return Task.FromResult<object?>(rule);
	}


	// All log levels.
	Logs.LogLevel[] LogLevels { get; } = Enum.GetValues<Logs.LogLevel>();


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var rule = this.Rule;
		if (rule == null)
		{
			this.levelComboBox.SelectedItem = Logs.LogLevel.Undefined;
			this.resultTypeComboBox.SelectedItem = DisplayableLogAnalysisResultType.Information;
		}
		else
		{
			this.levelComboBox.SelectedItem = rule.Level;
			this.messageTextBox.Text = rule.Message;
			this.patternEditor.Pattern = rule.Pattern;
			this.resultTypeComboBox.SelectedItem = rule.ResultType;
		}
		this.SynchronizationContext.Post(() =>
		{
			if (!this.patternEditor.ShowTutorialIfNeeded(this.Get<TutorialPresenter>("tutorialPresenter")))
				this.patternEditor.Focus();
		});
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && this.patternEditor.Pattern != null && !string.IsNullOrEmpty(this.messageTextBox.Text);
	

	// Available result types.
	DisplayableLogAnalysisResultType[] ResultTypes { get; } = Enum.GetValues<DisplayableLogAnalysisResultType>();
	

	/// <summary>
	/// Get of set rule to be edited.
	/// </summary>
	public KeyLogAnalysisRuleSet.Rule? Rule { get; set; }
}
