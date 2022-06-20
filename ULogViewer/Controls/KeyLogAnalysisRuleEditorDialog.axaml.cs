using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="KeyLogAnalysisRuleSet.Rule"/>.
/// </summary>
partial class KeyLogAnalysisRuleEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly StringInterpolationFormatTextBox messageTextBox;
	Regex? patternRegex;
	readonly TextBox patternTextBox;
	readonly ComboBox resultTypeComboBox;


	/// <summary>
	/// Initialize new <see cref="KeyLogAnalysisRuleEditorDialog"/> instance.
	/// </summary>
	public KeyLogAnalysisRuleEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.messageTextBox = this.FindControl<StringInterpolationFormatTextBox>(nameof(messageTextBox))!.Also(it =>
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
		this.patternTextBox = this.FindControl<TextBox>(nameof(patternTextBox)).AsNonNull();
		this.resultTypeComboBox = this.FindControl<ComboBox>(nameof(resultTypeComboBox)).AsNonNull();
	}


	// Copy pattern.
	void CopyPattern(TextBox textBox) =>
		textBox.CopyTextIfNotEmpty();


	// Edit pattern.
	async void EditPattern()
	{
		var regex = await new RegexEditorDialog()
		{
			InitialRegex = this.patternRegex,
			IsCapturingGroupsEnabled = true,
		}.ShowDialog<Regex?>(this);
		if (regex != null)
		{
			this.patternRegex = regex;
			this.patternTextBox.Text = regex.ToString();
			this.InvalidateInput();
		}
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var rule = new KeyLogAnalysisRuleSet.Rule(this.patternRegex.AsNonNull(), (DisplayableLogAnalysisResultType)this.resultTypeComboBox.SelectedItem!, this.messageTextBox.Text.AsNonNull());
		if (rule.Equals(this.Rule))
			return Task.FromResult<object?>(this.Rule);
		return Task.FromResult<object?>(rule);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var rule = this.Rule;
		var editPatternButton = this.FindControl<Control>("editPatternButton").AsNonNull();
		if (rule == null)
			this.resultTypeComboBox.SelectedItem = DisplayableLogAnalysisResultType.Information;
		else
		{
			this.messageTextBox.Text = rule.Message;
			this.patternRegex = rule.Pattern;
			this.patternTextBox.Text = rule.Pattern.ToString();
			this.resultTypeComboBox.SelectedItem = rule.ResultType;
		}
		if (!this.Application.PersistentState.GetValueOrDefault(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey))
		{
			this.FindControl<TutorialPresenter>("tutorialPresenter")!.ShowTutorial(new Tutorial().Also(it =>
			{
				it.Anchor = editPatternButton;
				it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/RegexEditorDialog.Tutorial.ClickButtonToEditPattern"));
				it.Dismissed += (_, e) =>
				{
					this.Application.PersistentState.SetValue<bool>(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey, true);
					editPatternButton.Focus();
				};
				it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
				it.IsSkippingAllTutorialsAllowed = false;
			}));
		}
		else
			this.SynchronizationContext.Post(editPatternButton.Focus);
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && this.patternRegex != null && !string.IsNullOrEmpty(this.messageTextBox.Text);
	

	/// <summary>
	/// Get of set rule to be edited.
	/// </summary>
	public KeyLogAnalysisRuleSet.Rule? Rule { get; set; }
}
