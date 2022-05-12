using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="KeyLogAnalysisRuleSet"/>.
/// </summary>
partial class KeyLogAnalysisRuleSetEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly ComboBox iconComboBox;
	readonly TextBox nameTextBox;
	readonly Avalonia.Controls.ListBox ruleListBox;
	readonly ObservableList<KeyLogAnalysisRuleSet.Rule> rules = new();


	/// <summary>
	/// Initialize new <see cref="KeyLogAnalysisRuleSetEditorDialog"/> instance.
	/// </summary>
	public KeyLogAnalysisRuleSetEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.iconComboBox = this.FindControl<ComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.FindControl<TextBox>(nameof(nameTextBox))!.Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.ruleListBox = this.FindControl<CarinaStudio.AppSuite.Controls.ListBox>(nameof(ruleListBox))!.Also(it =>
		{
			it.DoubleClickOnItem += (_, e) => this.EditRule((KeyLogAnalysisRuleSet.Rule)e.Item);
			it.SelectionChanged += (_, e) =>
			{
				this.SynchronizationContext.Post(() =>
				{
					it.SelectedItem?.Let(item =>
						it.ScrollIntoView(item));
				});
			};
		});
		this.rules.CollectionChanged += (_, e) => this.InvalidateInput();
	}


	// Add rule.
	async void AddRule()
	{
		var rule = await new KeyLogAnalysisRuleEditorDialog().ShowDialog<KeyLogAnalysisRuleSet.Rule?>(this);
		if (rule != null)
		{
			this.rules.Add(rule);
			this.ruleListBox.SelectedItem = rule;
			this.ruleListBox.Focus();
		}
	}


	// Edit rule.
	void EditRule(ListBoxItem item)
	{
		if (item.DataContext is not KeyLogAnalysisRuleSet.Rule rule)
			return;
		this.EditRule(rule);
	}
	async void EditRule(KeyLogAnalysisRuleSet.Rule rule)
	{
		var newRule = await new KeyLogAnalysisRuleEditorDialog()
		{
			Rule = rule,
		}.ShowDialog<KeyLogAnalysisRuleSet.Rule?>(this);
		if (newRule == null || newRule.Equals(rule))
			return;
		var index = this.rules.IndexOf(rule);
		if (index >= 0)
		{
			this.rules[index] = newRule;
			this.ruleListBox.SelectedItem = newRule;
			this.ruleListBox.Focus();
		}
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var ruleSet = this.RuleSet ?? new KeyLogAnalysisRuleSet(this.Application);
		ruleSet.Icon = (LogProfileIcon)this.iconComboBox.SelectedItem!;
		ruleSet.Name = this.nameTextBox.Text.AsNonNull();
		ruleSet.Rules = this.rules;
		return Task.FromResult<object?>(ruleSet);
	}


	// Get available icons.
	LogProfileIcon[] Icons { get; } = Enum.GetValues<LogProfileIcon>();


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var ruleSet = this.RuleSet;
		if (ruleSet != null)
		{
			this.iconComboBox.SelectedItem = ruleSet.Icon;
			this.nameTextBox.Text = ruleSet.Name;
			this.rules.AddRange(ruleSet.Rules);
		}
		else
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
		this.SynchronizationContext.Post(this.nameTextBox.Focus);
	}


	/// <inheritdoc/>
    protected override bool OnValidateInput() =>
		base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.nameTextBox.Text) && this.rules.IsNotEmpty();
	

	// Remove rule.
	void RemoveRule(ListBoxItem item)
	{
		if (item.DataContext is not KeyLogAnalysisRuleSet.Rule rule)
			return;
		var index = this.rules.IndexOf(rule);
		if (index >= 0)
		{
			this.rules.RemoveAt(index);
			this.ruleListBox.SelectedItem = null;
			this.ruleListBox.Focus();
		}
	}


	// Rules.
	IList<KeyLogAnalysisRuleSet.Rule> Rules { get => this.rules; }


	/// <summary>
	/// Get or set the rule set to be edited.
	/// </summary>
	public KeyLogAnalysisRuleSet? RuleSet { get; set; }
}
