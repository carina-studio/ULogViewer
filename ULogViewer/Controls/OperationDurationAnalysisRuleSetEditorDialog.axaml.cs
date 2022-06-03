using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="OperationDurationAnalysisRuleSet"/>.
	/// </summary>
	partial class OperationDurationAnalysisRuleSetEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
	{
		// Static fields.
		static readonly AvaloniaProperty<bool> AreValidParametersProperty = AvaloniaProperty.Register<OperationDurationAnalysisRuleSetEditorDialog, bool>("AreValidParameters");
		static readonly Dictionary<OperationDurationAnalysisRuleSet, OperationDurationAnalysisRuleSetEditorDialog> DialogWithEditingRuleSets = new();

		
		// Fields.
		readonly ComboBox iconComboBox;
		readonly TextBox nameTextBox;
		readonly ObservableList<OperationDurationAnalysisRuleSet.Rule> rules = new();
		readonly Avalonia.Controls.ListBox ruleListBox;
		OperationDurationAnalysisRuleSet? ruleSet;
		readonly ScheduledAction validateParametersAction;


		/// <summary>
		/// Initialize new <see cref="OperationDurationAnalysisRuleSetEditorDialog"/> instance.
		/// </summary>
		public OperationDurationAnalysisRuleSetEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.iconComboBox = this.Get<ComboBox>(nameof(iconComboBox));
			this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
			});
			this.ruleListBox = this.Get<AppSuite.Controls.ListBox>(nameof(ruleListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditRule((OperationDurationAnalysisRuleSet.Rule)e.Item);
			});
			this.rules.CollectionChanged += (_, e) => this.validateParametersAction!.Schedule();
			this.validateParametersAction = new(() =>
			{
				this.SetValue<bool>(AreValidParametersProperty, !string.IsNullOrWhiteSpace(this.nameTextBox.Text) && this.rules.IsNotEmpty());
			});
		}


		// Add rule.
		async void AddRule()
		{
			var rule = await new OperationDurationAnalysisRuleEditorDialog().ShowDialog<OperationDurationAnalysisRuleSet.Rule?>(this);
			if (rule == null)
				return;
			this.rules.Add(rule);
			this.ruleListBox.SelectedItem = rule;
			this.ruleListBox.Focus();
		}


		// Complete editing.
		void CompleteEditing()
		{
			// validate parameters
			this.validateParametersAction.ExecuteIfScheduled();
			if (!this.GetValue<bool>(AreValidParametersProperty))
				return;
			
			// create rule set
			var ruleSet = this.ruleSet ?? new OperationDurationAnalysisRuleSet(this.Application, "");
			ruleSet.Icon = (LogProfileIcon)this.iconComboBox.SelectedItem.AsNonNull();
			ruleSet.Name = this.nameTextBox.Text.AsNonNull();
			ruleSet.Rules = this.rules;

			// add rule set
			if (!OperationDurationAnalysisRuleSetManager.Default.RuleSets.Contains(ruleSet))
				OperationDurationAnalysisRuleSetManager.Default.AddRuleSet(ruleSet);

			// close window
			this.Close();
		}


		// Edit rule.
		void EditRule(ListBoxItem item)
		{
			if (item.DataContext is OperationDurationAnalysisRuleSet.Rule rule)
				this.EditRule(rule);
		}
		async void EditRule(OperationDurationAnalysisRuleSet.Rule rule)
		{
			var index = this.rules.IndexOf(rule);
			if (index < 0)
				return;
			var newRule = await new OperationDurationAnalysisRuleEditorDialog()
			{
				Rule = rule,
			}.ShowDialog<OperationDurationAnalysisRuleSet.Rule?>(this);
			if (newRule == null || newRule == rule)
				return;
			this.rules[index] = newRule;
			this.ruleListBox.SelectedItem = newRule;
			this.ruleListBox.Focus();
		}


		// Available icons.
		LogProfileIcon[] Icons { get; } = Enum.GetValues<LogProfileIcon>();


		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			if (this.ruleSet != null)
				DialogWithEditingRuleSets.Remove(this.ruleSet);
			base.OnClosed(e);
		}


		// Dialog opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var ruleSet = this.ruleSet;
			if (ruleSet != null)
			{
				this.iconComboBox.SelectedItem = ruleSet.Icon;
				this.nameTextBox.Text = ruleSet.Name;
				this.rules.AddAll(ruleSet.Rules);
			}
			else
				this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
			this.validateParametersAction.Schedule();
			this.SynchronizationContext.Post(this.nameTextBox.Focus);
		}


		/// <inheritdoc/>
		protected override WindowTransparencyLevel OnSelectTransparentLevelHint() =>
			WindowTransparencyLevel.None;
		

		// Remove rule.
		void RemoveRule(ListBoxItem item)
		{
			if (item.DataContext is not OperationDurationAnalysisRuleSet.Rule rule)
				return;
			this.rules.Remove(rule);
			this.ruleListBox.Focus();
		}
		

		// Rules.
		IList<OperationDurationAnalysisRuleSet.Rule> Rules { get => this.rules; }


		/// <summary>
		/// Show dialog to edit given rule set.
		/// </summary>
		/// <param name="parent">Parent window.</param>
		/// <param name="ruleSet">Rule set to edit.</param>
		public static void Show(Avalonia.Controls.Window parent, OperationDurationAnalysisRuleSet? ruleSet)
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
				ruleSet = ruleSet
			};
			if (ruleSet != null)
				DialogWithEditingRuleSets[ruleSet] = dialog;
			dialog.Show(parent);
		}
	}
}
