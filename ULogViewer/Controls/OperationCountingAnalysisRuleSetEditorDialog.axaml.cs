using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="OperationCountingAnalysisRuleSet"/>.
/// </summary>
class OperationCountingAnalysisRuleSetEditorDialog : AppSuite.Controls.Dialog<IULogViewerApplication>
{
	/// <summary>
	/// Definition set of patterns of rule.
	/// </summary>
	public static readonly SyntaxHighlightingDefinitionSet PatternDefinitionSet = RegexSyntaxHighlighting.CreateDefinitionSet(IAvaloniaApplication.Current);
	
	
	// Static fields.
	static readonly Dictionary<OperationCountingAnalysisRuleSet, OperationCountingAnalysisRuleSetEditorDialog> DialogWithEditingRuleSets = new();
	static readonly SettingKey<bool> DonotShowRestrictionsWithNonProVersionKey = new("OperationCountingAnalysisRuleSetEditorDialog.DonotShowRestrictionsWithNonProVersion");

	
	// Fields.
	readonly LogProfileIconColorComboBox iconColorComboBox;
	readonly LogProfileIconComboBox iconComboBox;
	readonly TextBox nameTextBox;
	readonly ObservableList<OperationCountingAnalysisRuleSet.Rule> rules = new();
	readonly Avalonia.Controls.ListBox ruleListBox;
	OperationCountingAnalysisRuleSet? ruleSet;


	/// <summary>
	/// Initialize new <see cref="OperationCountingAnalysisRuleSetEditorDialog"/> instance.
	/// </summary>
	public OperationCountingAnalysisRuleSetEditorDialog()
	{
		this.CopyRuleCommand = new Command<OperationCountingAnalysisRuleSet.Rule>(this.CopyRule);
		this.EditRuleCommand = new Command<OperationCountingAnalysisRuleSet.Rule>(this.EditRule);
		this.RemoveRuleCommand = new Command<OperationCountingAnalysisRuleSet.Rule>(this.RemoveRule);
		AvaloniaXamlLoader.Load(this);
		this.iconColorComboBox = this.Get<LogProfileIconColorComboBox>(nameof(iconColorComboBox));
		this.iconComboBox = this.Get<LogProfileIconComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox));
		this.ruleListBox = this.Get<AppSuite.Controls.ListBox>(nameof(ruleListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (sender, e) => _ = this.EditRule((OperationCountingAnalysisRuleSet.Rule)e.Item);
			it.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
			{
				if (!it.IsSelectedItemFocused)
					it.SelectedIndex = -1;
			});
			it.SelectionChanged += (_, _) =>
			{
				this.SynchronizationContext.Post(() =>
				{
					var index = it.SelectedIndex;
					if (index >= 0)
						it.ScrollIntoView(index);
				});
			};
		});
		this.AddHandler(KeyUpEvent, (sender, e) =>
		{
			if (this.ruleListBox.IsSelectedItemFocused && e.Key == Key.Enter)
				_ = this.EditRule((OperationCountingAnalysisRuleSet.Rule)this.ruleListBox.SelectedItem!);
		}, RoutingStrategies.Tunnel);
	}


	/// <summary>
	/// Add rule.
	/// </summary>
	public async Task AddRule()
	{
		var rule = await new OperationCountingAnalysisRuleEditorDialog().ShowDialog<OperationCountingAnalysisRuleSet.Rule?>(this);
		if (rule == null)
			return;
		this.rules.Add(rule);
		this.ruleListBox.SelectedItem = rule;
		this.ruleListBox.FocusSelectedItem();
	}


	/// <summary>
	/// Class all dialogs which are editing the given rule set.
	/// </summary>
	/// <param name="ruleSet">Rule set.</param>
	public static void CloseAll(OperationCountingAnalysisRuleSet ruleSet)
	{
		if (DialogWithEditingRuleSets.TryGetValue(ruleSet, out var dialog))
			dialog.Close();
	}


	/// <summary>
	/// Complete editing.
	/// </summary>
	public async Task CompleteEditing()
	{
		// validate parameters
		if (string.IsNullOrWhiteSpace(this.nameTextBox.Text))
		{
			this.HintForInput(this.Get<ScrollViewer>("contentScrollViewer"), this.Get<Control>("nameItem"), this.nameTextBox);
			return;
		}
		if (this.rules.IsEmpty())
		{
			this.HintForInput(this.Get<ScrollViewer>("contentScrollViewer"), this.Get<Control>("rulesItem"), this.Get<Control>("addRuleButton"));
			return;
		}
		
		// create rule set
		var ruleSet = this.ruleSet ?? new OperationCountingAnalysisRuleSet(this.Application);
		ruleSet.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
		ruleSet.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
		ruleSet.Name = this.nameTextBox.Text.AsNonNull();
		ruleSet.Rules = this.rules;

		// add rule set
		if (!OperationCountingAnalysisRuleSetManager.Default.RuleSets.Contains(ruleSet))
		{
			if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
				&& !OperationCountingAnalysisRuleSetManager.Default.CanAddRuleSet)
			{
				await new MessageDialog()
				{
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/DisplayableLogAnalysisRuleSetEditorDialog.CannotAddMoreRuleSetWithoutProVersion"),
				}.ShowDialog(this);
				return;
			}
			OperationCountingAnalysisRuleSetManager.Default.AddRuleSet(ruleSet);
		}

		// close window
		this.Close();
	}


	// Copy rule.
	async Task CopyRule(OperationCountingAnalysisRuleSet.Rule rule)
	{
		// get rule
		var index = this.rules.IndexOf(rule);
		if (index < 0)
			return;
		
		// edit rule
		var selectedOperationName = Utility.GenerateName(rule.OperationName, name => 
			this.rules.FirstOrDefault(it => it.OperationName == name) != null);
		var newRule = await new OperationCountingAnalysisRuleEditorDialog
		{
			Rule = new OperationCountingAnalysisRuleSet.Rule(rule, selectedOperationName),
		}.ShowDialog<OperationCountingAnalysisRuleSet.Rule?>(this);
		
		// add rule
		if (newRule != null)
		{
			this.rules.Add(newRule);
			this.ruleListBox.SelectedItem = newRule;
		}
		else
			this.ruleListBox.SelectedItem = rule;
		this.ruleListBox.FocusSelectedItem();
	}


	/// <summary>
	/// Command to copy rule.
	/// </summary>
	public ICommand CopyRuleCommand { get; }


	// Edit rule.
	async Task EditRule(OperationCountingAnalysisRuleSet.Rule rule)
	{
		var index = this.rules.IndexOf(rule);
		if (index < 0)
			return;
		var newRule = await new OperationCountingAnalysisRuleEditorDialog
		{
			Rule = rule,
		}.ShowDialog<OperationCountingAnalysisRuleSet.Rule?>(this);
		// ReSharper disable once PossibleUnintendedReferenceComparison
		if (newRule is not null && newRule != rule)
		{
			this.rules[index] = newRule;
			this.ruleListBox.SelectedItem = newRule;
		}
		else
			this.ruleListBox.SelectedItem = rule;
		this.ruleListBox.FocusSelectedItem();
	}


	/// <summary>
	/// Command to edit rule.
	/// </summary>
	public ICommand EditRuleCommand { get; }


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.ruleSet != null)
			DialogWithEditingRuleSets.Remove(this.ruleSet);
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() => this.nameTextBox.Focus());
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		var ruleSet = this.ruleSet;
		if (ruleSet is not null)
		{
			this.iconColorComboBox.SelectedItem = ruleSet.IconColor;
			this.iconComboBox.SelectedItem = ruleSet.Icon;
			this.nameTextBox.Text = ruleSet.Name;
			this.rules.AddAll(ruleSet.Rules);
		}
		else
		{
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
			if (!this.Application.ProductManager.IsProductActivated(Products.Professional))
			{
				this.SynchronizationContext.Post(async () =>
				{
					if (!OperationCountingAnalysisRuleSetManager.Default.CanAddRuleSet)
					{
						await new MessageDialog
						{
							Icon = MessageDialogIcon.Warning,
							Message = this.GetResourceObservable("String/DisplayableLogAnalysisRuleSetEditorDialog.CannotAddMoreRuleSetWithoutProVersion"),
						}.ShowDialog(this);
						this.IsEnabled = false;
						this.SynchronizationContext.PostDelayed(this.Close, 300); // [Workaround] Prevent crashing on macOS.
					}
					else if (!this.PersistentState.GetValueOrDefault(DonotShowRestrictionsWithNonProVersionKey))
					{
						var messageDialog = new MessageDialog
						{
							DoNotAskOrShowAgain = false,
							Icon = MessageDialogIcon.Information,
							Message = this.GetResourceObservable("String/DisplayableLogAnalysisRuleSetEditorDialog.RestrictionsOfNonProVersion"),
						};
						await messageDialog.ShowDialog(this);
						if (messageDialog.DoNotAskOrShowAgain == true)
							this.PersistentState.SetValue(DonotShowRestrictionsWithNonProVersionKey, true);
					}
				});
			}
		}
	}
	

	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#OperationCountingAnalysis");
#pragma warning restore CA1822
	

	// Remove rule.
	void RemoveRule(OperationCountingAnalysisRuleSet.Rule rule)
	{
		this.rules.Remove(rule);
		this.ruleListBox.Focus();
	}


	/// <summary>
	/// Command to remove rule.
	/// </summary>
	public ICommand RemoveRuleCommand { get; }
	

	/// <summary>
	/// Rules.
	/// </summary>
	public IList<OperationCountingAnalysisRuleSet.Rule> Rules => this.rules;


	/// <summary>
	/// Show dialog to edit given rule set.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="ruleSet">Rule set to edit.</param>
	public static void Show(Avalonia.Controls.Window parent, OperationCountingAnalysisRuleSet? ruleSet)
	{
		// show existing dialog
		if (ruleSet != null && DialogWithEditingRuleSets.TryGetValue(ruleSet, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}

		// show dialog
		dialog = new()
		{
			ruleSet = ruleSet
		};
		if (ruleSet != null)
			DialogWithEditingRuleSets[ruleSet] = dialog;
		dialog.Show(parent);
	}
}