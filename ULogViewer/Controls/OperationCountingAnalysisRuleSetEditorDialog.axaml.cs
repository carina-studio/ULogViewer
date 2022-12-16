using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="OperationCountingAnalysisRuleSet"/>.
	/// </summary>
	partial class OperationCountingAnalysisRuleSetEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
	{
		// Static fields.
		static readonly StyledProperty<bool> AreValidParametersProperty = AvaloniaProperty.Register<OperationDurationAnalysisRuleSetEditorDialog, bool>("AreValidParameters");
		static readonly Dictionary<OperationCountingAnalysisRuleSet, OperationCountingAnalysisRuleSetEditorDialog> DialogWithEditingRuleSets = new();
		static readonly SettingKey<bool> DonotShowRestrictionsWithNonProVersionKey = new("OperationCountingAnalysisRuleSetEditorDialog.DonotShowRestrictionsWithNonProVersion");

		
		// Fields.
		readonly LogProfileIconColorComboBox iconColorComboBox;
		readonly LogProfileIconComboBox iconComboBox;
		readonly TextBox nameTextBox;
		readonly ObservableList<OperationCountingAnalysisRuleSet.Rule> rules = new();
		readonly Avalonia.Controls.ListBox ruleListBox;
		OperationCountingAnalysisRuleSet? ruleSet;
		readonly ScheduledAction validateParametersAction;


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
			this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
			});
			this.ruleListBox = this.Get<AppSuite.Controls.ListBox>(nameof(ruleListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditRule((OperationCountingAnalysisRuleSet.Rule)e.Item);
			});
			this.rules.CollectionChanged += (_, e) => this.validateParametersAction!.Schedule();
			this.validateParametersAction = new(() =>
			{
				this.SetValue<bool>(AreValidParametersProperty, !string.IsNullOrWhiteSpace(this.nameTextBox.Text) && this.rules.IsNotEmpty());
			});
		}


		/// <summary>
		/// Add rule.
		/// </summary>
		public async void AddRule()
		{
			var rule = await new OperationCountingAnalysisRuleEditorDialog().ShowDialog<OperationCountingAnalysisRuleSet.Rule?>(this);
			if (rule == null)
				return;
			this.rules.Add(rule);
			this.ruleListBox.SelectedItem = rule;
			this.ruleListBox.Focus();
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
		public async void CompleteEditing()
		{
			// validate parameters
			this.validateParametersAction.ExecuteIfScheduled();
			if (!this.GetValue<bool>(AreValidParametersProperty))
				return;
			
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
		async void CopyRule(OperationCountingAnalysisRuleSet.Rule rule)
		{
			// get rule
			var index = this.rules.IndexOf(rule);
			if (index < 0)
				return;
			
			// edit rule
			var selectedOperationName = Utility.GenerateName(rule.OperationName, name => 
				this.rules.FirstOrDefault(it => it.OperationName == name) != null);
			var newRule = await new OperationCountingAnalysisRuleEditorDialog()
			{
				Rule = new OperationCountingAnalysisRuleSet.Rule(rule, selectedOperationName),
			}.ShowDialog<OperationCountingAnalysisRuleSet.Rule?>(this);
			
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
		async void EditRule(OperationCountingAnalysisRuleSet.Rule rule)
		{
			var index = this.rules.IndexOf(rule);
			if (index < 0)
				return;
			var newRule = await new OperationCountingAnalysisRuleEditorDialog()
			{
				Rule = rule,
			}.ShowDialog<OperationCountingAnalysisRuleSet.Rule?>(this);
			if (newRule == null || newRule == rule)
				return;
			this.rules[index] = newRule;
			this.ruleListBox.SelectedItem = newRule;
			this.ruleListBox.Focus();
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


		// Dialog opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var ruleSet = this.ruleSet;
			if (ruleSet != null)
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
			this.validateParametersAction.Schedule();
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
			Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#OperationDurationAnalysis");
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
		public IList<OperationCountingAnalysisRuleSet.Rule> Rules { get => this.rules; }


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
