using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="OperationDurationAnalysisRuleSet.Rule"/>.
	/// </summary>
	partial class OperationDurationAnalysisRuleEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ObservableList<ContextualBasedAnalysisCondition> beginningConditions = new();
		Regex? beginningPattern;
		readonly TextBox beginningPatternTextBox;
		readonly ObservableList<ContextualBasedAnalysisAction> beginningPostActions = new();
		readonly ObservableList<ContextualBasedAnalysisAction> beginningPreActions = new();
		readonly ObservableList<ContextualBasedAnalysisCondition> endingConditions = new();
		readonly ComboBox endingOrderComboBox;
		Regex? endingPattern;
		readonly TextBox endingPatternTextBox;
		readonly ObservableList<ContextualBasedAnalysisAction> endingPostActions = new();
		readonly ObservableList<ContextualBasedAnalysisAction> endingPreActions = new();
		readonly TextBox operationNameTextBox;


		/// <summary>
		/// Initialize new <see cref="OperationDurationAnalysisRuleEditorDialog"/> instance.
		/// </summary>
		public OperationDurationAnalysisRuleEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.beginningPatternTextBox = this.Get<TextBox>(nameof(beginningPatternTextBox));
			this.endingOrderComboBox = this.Get<ComboBox>(nameof(endingOrderComboBox));
			this.endingPatternTextBox = this.Get<TextBox>(nameof(endingPatternTextBox));
			this.operationNameTextBox = this.Get<TextBox>(nameof(operationNameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
		}


		// Beginning conditions.
		IList<ContextualBasedAnalysisCondition> BeginningConditions { get => this.beginningConditions; }


		// Post-actions for beginning of operation.
		IList<ContextualBasedAnalysisAction> BeginningPostActions { get => this.beginningPostActions; }


		// Pre-actions for beginning of operation.
		IList<ContextualBasedAnalysisAction> BeginningPreActions { get => this.beginningPreActions; }


		// Edit beginning patten.
		async void EditBeginningPattern()
		{
			var newPattern = await new RegexEditorDialog()
			{
				InitialRegex = this.beginningPattern,
				IsCapturingGroupsEnabled = true,
			}.ShowDialog<Regex?>(this);
			if (newPattern == null)
				return;
			this.beginningPattern = newPattern;
			this.beginningPatternTextBox.Text = newPattern.ToString();
			this.InvalidateInput();
		}


		// Edit ending patten.
		async void EditEndingPattern()
		{
			var newPattern = await new RegexEditorDialog()
			{
				InitialRegex = this.endingPattern,
				IsCapturingGroupsEnabled = true,
			}.ShowDialog<Regex?>(this);
			if (newPattern == null)
				return;
			this.endingPattern = newPattern;
			this.endingPatternTextBox.Text = newPattern.ToString();
			this.InvalidateInput();
		}


		// Ending conditions.
		IList<ContextualBasedAnalysisCondition> EndingConditions { get => this.endingConditions; }


		// Post-actions for ending of operation.
		IList<ContextualBasedAnalysisAction> EndingPostActions { get => this.endingPostActions; }


		// Pre-actions for ending of operation.
		IList<ContextualBasedAnalysisAction> EndingPreActions { get => this.endingPreActions; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var rule = this.Rule;
			var newRule = new OperationDurationAnalysisRuleSet.Rule(this.operationNameTextBox.Text.AsNonNull(),
				this.beginningPattern.AsNonNull(),
				this.beginningPreActions,
				this.beginningConditions,
				this.beginningPostActions,
				this.endingPattern.AsNonNull(),
				this.endingPreActions,
				this.endingConditions,
				this.endingPostActions,
				(OperationEndingOrder)this.endingOrderComboBox.SelectedItem.AsNonNull());
			if (rule == newRule)
				return Task.FromResult<object?>(rule);
			return Task.FromResult<object?>(newRule);
		}


		// Dialog opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var rule = this.Rule;
			if (rule != null)
			{
				this.operationNameTextBox.Text = rule.OperationName;
				this.beginningConditions.AddAll(rule.BeginningConditions);
				this.beginningPattern = rule.BeginningPattern;
				this.beginningPatternTextBox.Text = rule.BeginningPattern.ToString();
				this.beginningPreActions.AddAll(rule.BeginningPreActions);
				this.beginningPostActions.AddAll(rule.BeginningPostActions);
				this.endingConditions.AddAll(rule.EndingConditions);
				this.endingOrderComboBox.SelectedItem = rule.EndingOrder;
				this.endingPattern = rule.EndingPattern;
				this.endingPatternTextBox.Text = rule.EndingPattern.ToString();
				this.endingPreActions.AddAll(rule.EndingPreActions);
				this.endingPostActions.AddAll(rule.EndingPostActions);
			}
			else
				this.endingOrderComboBox.SelectedItem = OperationEndingOrder.FirstInFirstOut;
			if (!this.Application.PersistentState.GetValueOrDefault(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey))
			{
				this.FindControl<TutorialPresenter>("tutorialPresenter")!.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.Get<Control>("editBeginningPatternButton");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/RegexEditorDialog.Tutorial.ClickButtonToEditPattern"));
					it.Dismissed += (_, e) =>
					{
						this.Application.PersistentState.SetValue<bool>(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey, true);
						this.operationNameTextBox.Focus();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.IsSkippingAllTutorialsAllowed = false;
				}));
			}
			else
				this.SynchronizationContext.Post(this.operationNameTextBox.Focus);
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() 
			&& !string.IsNullOrWhiteSpace(this.operationNameTextBox.Text) 
			&& this.beginningPattern != null
			&& this.endingPattern != null;
		

		/// <summary>
		/// Get or set rule to be edited.
		/// </summary>
		public OperationDurationAnalysisRuleSet.Rule? Rule { get; set; }
	}
}
