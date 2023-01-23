using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="KeyLogAnalysisRuleSet.Rule"/>.
/// </summary>
partial class KeyLogAnalysisRuleEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly ComboBox byteSizeUnitComboBox;
	readonly TextBox byteSizeVarNameTextBox;
	readonly ObservableList<DisplayableLogAnalysisCondition> conditions = new();
	readonly ComboBox durationUnitComboBox;
	readonly TextBox durationVarNameTextBox;
	readonly ComboBox levelComboBox;
	readonly StringInterpolationFormatTextBox messageTextBox;
	readonly PatternEditor patternEditor;
	readonly TextBox quantityVarNameTextBox;
	readonly ComboBox resultTypeComboBox;


	/// <summary>
	/// Initialize new <see cref="KeyLogAnalysisRuleEditorDialog"/> instance.
	/// </summary>
	public KeyLogAnalysisRuleEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.byteSizeUnitComboBox = this.Get<ComboBox>(nameof(byteSizeUnitComboBox));
		this.byteSizeVarNameTextBox = this.Get<TextBox>(nameof(byteSizeVarNameTextBox));
		this.durationUnitComboBox = this.Get<ComboBox>(nameof(durationUnitComboBox));
		this.durationVarNameTextBox = this.Get<TextBox>(nameof(durationVarNameTextBox));
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
		this.quantityVarNameTextBox = this.Get<TextBox>(nameof(quantityVarNameTextBox));
		this.resultTypeComboBox = this.Get<ComboBox>(nameof(resultTypeComboBox));
	}


	/// <summary>
	/// Conditions to match log.
	/// </summary>
	public IList<DisplayableLogAnalysisCondition> Conditions { get => this.conditions; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var rule = new KeyLogAnalysisRuleSet.Rule(this.patternEditor.Pattern.AsNonNull(), 
			(Logs.LogLevel)this.levelComboBox.SelectedItem!,
			this.conditions,
			(DisplayableLogAnalysisResultType)this.resultTypeComboBox.SelectedItem!, 
			this.messageTextBox.Text.AsNonNull(),
			this.byteSizeVarNameTextBox.Text,
			(IO.FileSizeUnit)this.byteSizeUnitComboBox.SelectedItem!,
			this.durationVarNameTextBox.Text,
			(TimeSpanUnit)this.durationUnitComboBox.SelectedItem!,
			this.quantityVarNameTextBox.Text);
		if (rule.Equals(this.Rule))
			return Task.FromResult<object?>(this.Rule);
		return Task.FromResult<object?>(rule);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var rule = this.Rule;
		if (rule == null)
		{
			this.byteSizeUnitComboBox.SelectedItem = default(IO.FileSizeUnit);
			this.durationUnitComboBox.SelectedItem = default(TimeSpanUnit);
			this.levelComboBox.SelectedItem = Logs.LogLevel.Undefined;
			this.resultTypeComboBox.SelectedItem = DisplayableLogAnalysisResultType.Information;
		}
		else
		{
			this.byteSizeUnitComboBox.SelectedItem = rule.ByteSizeUnit;
			this.byteSizeVarNameTextBox.Text = rule.ByteSizeVariableName;
			this.conditions.AddAll(rule.Conditions);
			this.durationUnitComboBox.SelectedItem = rule.DurationUnit;
			this.durationVarNameTextBox.Text = rule.DurationVariableName;
			this.levelComboBox.SelectedItem = rule.Level;
			this.messageTextBox.Text = rule.Message;
			this.patternEditor.Pattern = rule.Pattern;
			this.quantityVarNameTextBox.Text = rule.QuantityVariableName;
			this.resultTypeComboBox.SelectedItem = rule.ResultType;
		}
		this.SynchronizationContext.Post(() =>
		{
			if (!this.patternEditor.ShowTutorialIfNeeded(this.Get<TutorialPresenter>("tutorialPresenter"), this.patternEditor))
				this.patternEditor.Focus();
		});
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && this.patternEditor.Pattern != null && !string.IsNullOrEmpty(this.messageTextBox.Text);
	

	/// <summary>
	/// Get of set rule to be edited.
	/// </summary>
	public KeyLogAnalysisRuleSet.Rule? Rule { get; set; }
}
