using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;


/// <summary>
/// Dialog to edit <see cref="KeyLogAnalysisRuleSet"/>.
/// </summary>
partial class KeyLogAnalysisRuleSetEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
{
	// Static fields.
	static readonly StyledProperty<bool> AreValidParametersProperty = AvaloniaProperty.Register<KeyLogAnalysisRuleSetEditorDialog, bool>("AreValidParameters");
	static readonly Dictionary<KeyLogAnalysisRuleSet, KeyLogAnalysisRuleSetEditorDialog> DialogWithEditingRuleSets = new();
	static readonly SettingKey<bool> DonotShowRestrictionsWithNonProVersionKey = new("KeyLogAnalysisRuleSetEditorDialog.DonotShowRestrictionsWithNonProVersion");


	// Fields.
	KeyLogAnalysisRuleSet? editingRuleSet;
	readonly LogProfileIconColorComboBox iconColorComboBox;
	readonly LogProfileIconComboBox iconComboBox;
	readonly TextBox nameTextBox;
	readonly Avalonia.Controls.ListBox ruleListBox;
	readonly ObservableList<KeyLogAnalysisRuleSet.Rule> rules = new();
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="KeyLogAnalysisRuleSetEditorDialog"/> instance.
	/// </summary>
	public KeyLogAnalysisRuleSetEditorDialog()
	{
		var b = this.FindResource("TextControlForeground");
		this.CopyRuleCommand = new Command<ListBoxItem>(this.CopyRule);
		this.EditRuleCommand = new Command<ListBoxItem>(this.EditRule);
		this.RemoveRuleCommand = new Command<ListBoxItem>(this.RemoveRule);
		this.MessageSyntaxHighlightingDefinitionSet = StringInterpolationFormatSyntaxHighlighting.CreateDefinitionSet(this.Application);
		this.RegexSyntaxHighlightingDefinitionSet = RegexSyntaxHighlighting.CreateDefinitionSet(this.Application);
		AvaloniaXamlLoader.Load(this);
		this.iconColorComboBox = this.Get<LogProfileIconColorComboBox>(nameof(iconColorComboBox));
		this.iconComboBox = this.Get<LogProfileIconComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.ruleListBox = this.Get<CarinaStudio.AppSuite.Controls.ListBox>(nameof(ruleListBox)).Also(it =>
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


	/// <summary>
	/// Add rule.
	/// </summary>
	public async void AddRule()
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


	/// <summary>
	/// Complete editing.
	/// </summary>
	public async void CompleteEditing()
	{
		// validate parameters
		this.validateParametersAction.ExecuteIfScheduled();
		if (!this.GetValue<bool>(AreValidParametersProperty))
			return;
		
		// setup rule set
		var ruleSet = this.editingRuleSet ?? new KeyLogAnalysisRuleSet(this.Application);
		ruleSet.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
		ruleSet.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
		ruleSet.Name = this.nameTextBox.Text.AsNonNull();
		ruleSet.Rules = this.rules;

		// add rule set
		if (!KeyLogAnalysisRuleSetManager.Default.RuleSets.Contains(ruleSet))
		{
			if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
				&& !KeyLogAnalysisRuleSetManager.Default.CanAddRuleSet)
			{
				await new MessageDialog()
				{
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/DisplayableLogAnalysisRuleSetEditorDialog.CannotAddMoreRuleSetWithoutProVersion"),
				}.ShowDialog(this);
				return;
			}
			KeyLogAnalysisRuleSetManager.Default.AddRuleSet(ruleSet);
		}

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


	/// <summary>
	/// Command to copy rule.
	/// </summary>
	public ICommand CopyRuleCommand { get; }


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


	/// <summary>
	/// Command to edit rule.
	/// </summary>
	public ICommand EditRuleCommand { get; }


	/// <summary>
	/// Definition set of message syntax highlighting.
	/// </summary>
	public SyntaxHighlightingDefinitionSet MessageSyntaxHighlightingDefinitionSet { get; }


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
			this.iconColorComboBox.SelectedItem = ruleSet.IconColor;
			this.iconComboBox.SelectedItem = ruleSet.Icon;
			this.nameTextBox.Text = ruleSet.Name;
			this.rules.AddRange(ruleSet.Rules);
		}
		else
		{
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
			if (!this.Application.ProductManager.IsProductActivated(Products.Professional))
			{
				this.SynchronizationContext.Post(async () =>
				{
					if (!KeyLogAnalysisRuleSetManager.Default.CanAddRuleSet)
					{
						await new MessageDialog()
						{
							Icon = MessageDialogIcon.Warning,
							Message = this.GetResourceObservable("String/DisplayableLogAnalysisRuleSetEditorDialog.CannotAddMoreRuleSetWithoutProVersion"),
						}.ShowDialog(this);
						this.IsEnabled = false;
						this.SynchronizationContext.PostDelayed(this.Close, 300); // [Workaround] Prevent crashing on macOS.
					}
					else if (!this.PersistentState.GetValueOrDefault(DonotShowRestrictionsWithNonProVersionKey))
					{
						var messageDialog = new MessageDialog()
						{
							DoNotAskOrShowAgain = false,
							Icon = MessageDialogIcon.Information,
							Message = this.GetResourceObservable("String/DisplayableLogAnalysisRuleSetEditorDialog.RestrictionsOfNonProVersion"),
						};
						await messageDialog.ShowDialog(this);
						if (messageDialog.DoNotAskOrShowAgain == true)
							this.PersistentState.SetValue<bool>(DonotShowRestrictionsWithNonProVersionKey, true);
					}
				});
			}
		}
		this.validateParametersAction.Execute();
		this.SynchronizationContext.Post(this.nameTextBox.Focus);
	}


	/// <inheritdoc/>
	protected override WindowTransparencyLevel OnSelectTransparentLevelHint() =>
		WindowTransparencyLevel.None;
	

	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#KeyLogAnalysis");
#pragma warning restore CA1822


	/// <summary>
	/// Definition set of regex syntax highlighting.
	/// </summary>
	public SyntaxHighlightingDefinitionSet RegexSyntaxHighlightingDefinitionSet { get; }


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


	/// <summary>
	/// Command to remove rule.
	/// </summary>
	/// <value></value>
	public ICommand RemoveRuleCommand { get; }


	/// <summary>
	/// Rules.
	/// </summary>
	public IList<KeyLogAnalysisRuleSet.Rule> Rules { get => this.rules; }


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
