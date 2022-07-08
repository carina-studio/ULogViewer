using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="KeyLogAnalysisRuleSet"/>.
/// </summary>
partial class KeyLogAnalysisRuleSetEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
{
	// Static fields.
	static readonly AvaloniaProperty<bool> AreValidParametersProperty = AvaloniaProperty.Register<KeyLogAnalysisRuleSetEditorDialog, bool>("AreValidParameters");
	static readonly Dictionary<KeyLogAnalysisRuleSet, KeyLogAnalysisRuleSetEditorDialog> DialogWithEditingRuleSets = new();


	// Fields.
	KeyLogAnalysisRuleSet? editingRuleSet;
	readonly ComboBox iconComboBox;
	readonly TextBox nameTextBox;
	readonly Avalonia.Controls.ListBox ruleListBox;
	readonly ObservableList<KeyLogAnalysisRuleSet.Rule> rules = new();
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="KeyLogAnalysisRuleSetEditorDialog"/> instance.
	/// </summary>
	public KeyLogAnalysisRuleSetEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.iconComboBox = this.FindControl<ComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.FindControl<TextBox>(nameof(nameTextBox))!.Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
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
		this.rules.CollectionChanged += (_, e) => this.validateParametersAction?.Schedule();
		this.validateParametersAction = new(() =>
		{
			if (this.IsClosed)
				return;
			if (string.IsNullOrWhiteSpace(this.nameTextBox.Text)
				|| this.rules.IsEmpty())
			{
				this.SetValue<bool>(AreValidParametersProperty, false);
			}
			else
				this.SetValue<bool>(AreValidParametersProperty, true);
		});
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


	/// <summary>
	/// Class all dialogs which are editing the given rule set.
	/// </summary>
	/// <param name="ruleSet">Rule set.</param>
	public static void CloseAll(KeyLogAnalysisRuleSet ruleSet)
	{
		if (DialogWithEditingRuleSets.TryGetValue(ruleSet, out var dialog))
			dialog.Close();
	}


	// Complete editing.
	void CompleteEditing()
	{
		// validate parameters
		this.validateParametersAction.ExecuteIfScheduled();
		if (!this.GetValue<bool>(AreValidParametersProperty))
			return;
		
		// setup rule set
		var ruleSet = this.editingRuleSet ?? new KeyLogAnalysisRuleSet(this.Application);
		ruleSet.Icon = (LogProfileIcon)this.iconComboBox.SelectedItem!;
		ruleSet.Name = this.nameTextBox.Text.AsNonNull();
		ruleSet.Rules = this.rules;

		// add rule set
		if (!KeyLogAnalysisRuleSetManager.Default.RuleSets.Contains(ruleSet))
			KeyLogAnalysisRuleSetManager.Default.AddRuleSet(ruleSet);

		// close window
		this.Close();
	}


	// Copy rule.
	async void CopyRule(ListBoxItem item)
	{
		// get rule
		var rule = (KeyLogAnalysisRuleSet.Rule)item.DataContext.AsNonNull();
		var index = this.rules.IndexOf(rule);
		if (index < 0)
			return;

		// edit rule
		var newRule = await new KeyLogAnalysisRuleEditorDialog()
		{
			Rule = rule,
		}.ShowDialog<KeyLogAnalysisRuleSet.Rule?>(this);
		
		// add rule
		if (newRule != null)
		{
			this.rules.Add(newRule);
			this.ruleListBox.SelectedItem = newRule;
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


	// Get available icons.
	LogProfileIcon[] Icons { get; } = Enum.GetValues<LogProfileIcon>();


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.editingRuleSet != null)
			DialogWithEditingRuleSets.Remove(this.editingRuleSet);
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var ruleSet = this.editingRuleSet;
		if (ruleSet != null)
		{
			this.iconComboBox.SelectedItem = ruleSet.Icon;
			this.nameTextBox.Text = ruleSet.Name;
			this.rules.AddRange(ruleSet.Rules);
		}
		else
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
		this.validateParametersAction.Execute();
		this.SynchronizationContext.Post(this.nameTextBox.Focus);
	}


	/// <inheritdoc/>
	protected override WindowTransparencyLevel OnSelectTransparentLevelHint() =>
		WindowTransparencyLevel.None;
	

	// Open online documentation.
	void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#KeyLogAnalysis");
	

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
	/// Show dialog to edit given rule set.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="ruleSet">Rule set to edit.</param>
	public static void Show(Avalonia.Controls.Window parent, KeyLogAnalysisRuleSet? ruleSet)
	{
		// show existing dialog
		if (ruleSet != null && DialogWithEditingRuleSets.TryGetValue(ruleSet, out var dialog))
		{
			dialog?.ActivateAndBringToFront();
			return;
		}

		// show dialog
		dialog = new()
		{
			editingRuleSet = ruleSet
		};
		if (ruleSet != null)
			DialogWithEditingRuleSets[ruleSet] = dialog;
		dialog.Show(parent);
	}
}
