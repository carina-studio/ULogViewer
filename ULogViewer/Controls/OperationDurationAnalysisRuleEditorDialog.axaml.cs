using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="OperationDurationAnalysisRuleSet.Rule"/>.
	/// </summary>
	partial class OperationDurationAnalysisRuleEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ObservableList<DisplayableLogAnalysisCondition> beginningConditions = new();
		readonly PatternEditor beginningPatternEditor;
		readonly ObservableList<ContextualBasedAnalysisAction> beginningPostActions = new();
		readonly ObservableList<ContextualBasedAnalysisAction> beginningPreActions = new();
		readonly ComboBox byteSizeUnitComboBox;
		readonly TextBox byteSizeVarNameTextBox;
		readonly TextBox customMessageTextBox;
		readonly ObservableList<DisplayableLogAnalysisCondition> endingConditions = new();
		readonly ComboBox endingModeComboBox;
		readonly PatternEditor endingPatternEditor;
		readonly ObservableList<ContextualBasedAnalysisAction> endingPostActions = new();
		readonly ObservableList<ContextualBasedAnalysisAction> endingPreActions = new();
		readonly Avalonia.Controls.ListBox endingVariableListBox;
		readonly ObservableList<string> endingVariables = new();
		readonly CarinaStudio.Controls.TimeSpanTextBox maxDurationTextBox;
		readonly CarinaStudio.Controls.TimeSpanTextBox minDurationTextBox;
		readonly TextBox operationNameTextBox;
		readonly TextBox quantityVarNameTextBox;
		readonly ComboBox resultTypeComboBox;


		/// <summary>
		/// Initialize new <see cref="OperationDurationAnalysisRuleEditorDialog"/> instance.
		/// </summary>
		public OperationDurationAnalysisRuleEditorDialog()
		{
			this.EditEndingVariableCommand = new Command<ListBoxItem>(this.EditEndingVariable);
			this.RemoveEndingVariableCommand = new Command<ListBoxItem>(this.RemoveEndingVariable);
			AvaloniaXamlLoader.Load(this);
			this.beginningPatternEditor = this.Get<PatternEditor>(nameof(beginningPatternEditor)).Also(it =>
			{
				it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.byteSizeUnitComboBox = this.Get<ComboBox>(nameof(byteSizeUnitComboBox));
			this.byteSizeVarNameTextBox = this.Get<TextBox>(nameof(byteSizeVarNameTextBox));
			this.customMessageTextBox = this.Get<TextBox>(nameof(customMessageTextBox));
			this.endingModeComboBox = this.Get<ComboBox>(nameof(endingModeComboBox));
			this.endingPatternEditor = this.Get<PatternEditor>(nameof(endingPatternEditor)).Also(it =>
			{
				it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.endingVariableListBox = this.Get<AppSuite.Controls.ListBox>(nameof(endingVariableListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditEndingVariable((string)e.Item);
			});
			this.maxDurationTextBox = this.Get<CarinaStudio.Controls.TimeSpanTextBox>(nameof(maxDurationTextBox)).Also(it =>
			{
				it.GetObservable(CarinaStudio.Controls.TimeSpanTextBox.IsTextValidProperty).Subscribe(_ => this.InvalidateInput());
				it.GetObservable(CarinaStudio.Controls.TimeSpanTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.minDurationTextBox = this.Get<CarinaStudio.Controls.TimeSpanTextBox>(nameof(minDurationTextBox)).Also(it =>
			{
				it.GetObservable(CarinaStudio.Controls.TimeSpanTextBox.IsTextValidProperty).Subscribe(_ => this.InvalidateInput());
				it.GetObservable(CarinaStudio.Controls.TimeSpanTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.operationNameTextBox = this.Get<TextBox>(nameof(operationNameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.quantityVarNameTextBox = this.Get<TextBox>(nameof(quantityVarNameTextBox));
			this.resultTypeComboBox = this.Get<ComboBox>(nameof(resultTypeComboBox));
		}


		/// <summary>
		/// Add ending variable.
		/// </summary>
		public async void AddEndingVariable()
		{
			var variable = await new TextInputDialog()
			{
				MaxTextLength = 256,
				Message = this.Application.GetString("OperationDurationAnalysisRuleEditorDialog.EndingVariable"),
			}.ShowDialog(this);
			if (string.IsNullOrWhiteSpace(variable))
				return;
			variable = variable.Trim();
			var index = this.endingVariables.IndexOf(variable);
			if (index < 0)
			{
				this.endingVariables.Add(variable);
				this.endingVariableListBox.SelectedIndex = this.endingVariables.Count - 1;
			}
			else
				this.endingVariableListBox.SelectedIndex = index;
			this.endingVariableListBox.Focus();
		}


		/// <summary>
		/// Beginning conditions.
		/// </summary>
		public IList<DisplayableLogAnalysisCondition> BeginningConditions { get => this.beginningConditions; }


		/// <summary>
		/// Post-actions for beginning of operation.
		/// </summary>
		public IList<ContextualBasedAnalysisAction> BeginningPostActions { get => this.beginningPostActions; }


		/// <summary>
		/// Pre-actions for beginning of operation.
		/// </summary>
		public IList<ContextualBasedAnalysisAction> BeginningPreActions { get => this.beginningPreActions; }


		/// <summary>
		/// Default beginning conditions.
		/// </summary>
		public IList<DisplayableLogAnalysisCondition>? DefaultBeginningConditions { get; set; }


		// Default pattern of beginning of operation.
		public Regex? DefaultBeginningPattern { get; set; }


		// Default post-actions for beginning of operation.
		public IList<ContextualBasedAnalysisAction>? DefaultBeginningPostActions { get; set; }

		
		// Default pre-actions for beginning of operation.
		public IList<ContextualBasedAnalysisAction>? DefaultBeginningPreActions { get; set; }


		// Default beginning conditions.
		public IList<DisplayableLogAnalysisCondition>? DefaultEndingConditions { get; set; }


		// Default pattern of ending of operation.
		public Regex? DefaultEndingPattern { get; set; }


		// Default post-actions for ending of operation.
		public IList<ContextualBasedAnalysisAction>? DefaultEndingPostActions { get; set; }


		// Default pre-actions for ending of operation.
		public IList<ContextualBasedAnalysisAction>? DefaultEndingPreActions { get; set; }


		// Default name of operation.
		public string? DefaultOperationName { get; set; }


		// Default type of analysis result.
		public DisplayableLogAnalysisResultType DefaultResultType { get; set; } = DisplayableLogAnalysisResultType.TimeSpan;


		// Edit ending variable.
		void EditEndingVariable(ListBoxItem item) =>
			this.EditEndingVariable((string)item.DataContext.AsNonNull());
		async void EditEndingVariable(string variable)
		{
			var index = this.endingVariables.IndexOf(variable);
			if (index < 0)
				return;
			var newVariable = await new TextInputDialog()
			{
				InitialText = variable,
				MaxTextLength = 256,
				Message = this.Application.GetString("OperationDurationAnalysisRuleEditorDialog.EndingVariable"),
			}.ShowDialog(this);
			if (string.IsNullOrWhiteSpace(newVariable))
				return;
			newVariable = newVariable.Trim();
			if (newVariable == variable)
				return;
			this.endingVariables[index] = newVariable;
			this.endingVariableListBox.SelectedIndex = index;
			this.endingVariableListBox.Focus();
		}


		/// <summary>
		/// Command to edit ending variable.
		/// </summary>
		public ICommand EditEndingVariableCommand { get; }


		/// <summary>
		/// Ending conditions.
		/// </summary>
		public IList<DisplayableLogAnalysisCondition> EndingConditions { get => this.endingConditions; }


		/// <summary>
		/// Post-actions for ending of operation.
		/// </summary>
		public IList<ContextualBasedAnalysisAction> EndingPostActions { get => this.endingPostActions; }


		/// <summary>
		/// Pre-actions for ending of operation.
		/// </summary>
		public IList<ContextualBasedAnalysisAction> EndingPreActions { get => this.endingPreActions; }


		/// <summary>
		/// Ending variables.
		/// </summary>
		public IList<string> EndingVariables { get => this.endingVariables; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var rule = this.Rule;
			var newRule = new OperationDurationAnalysisRuleSet.Rule(this.operationNameTextBox.Text.AsNonNull(),
				(DisplayableLogAnalysisResultType)this.resultTypeComboBox.SelectedItem.AsNonNull(),
				this.beginningPatternEditor.Pattern.AsNonNull(),
				this.beginningPreActions,
				this.beginningConditions,
				this.beginningPostActions,
				this.endingPatternEditor.Pattern.AsNonNull(),
				this.endingPreActions,
				this.endingConditions,
				this.endingPostActions,
				(OperationEndingMode)this.endingModeComboBox.SelectedItem.AsNonNull(),
				this.endingVariables,
				this.minDurationTextBox.Value,
				this.maxDurationTextBox.Value,
				this.customMessageTextBox.Text,
				this.byteSizeVarNameTextBox.Text,
				(IO.FileSizeUnit)this.byteSizeUnitComboBox.SelectedItem!,
				this.quantityVarNameTextBox.Text);
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
				this.resultTypeComboBox.SelectedItem = rule.ResultType;
				this.beginningConditions.AddAll(rule.BeginningConditions);
				this.beginningPatternEditor.Pattern = rule.BeginningPattern;
				this.beginningPreActions.AddAll(rule.BeginningPreActions);
				this.beginningPostActions.AddAll(rule.BeginningPostActions);
				this.byteSizeUnitComboBox.SelectedItem = rule.ByteSizeUnit;
				this.byteSizeVarNameTextBox.Text = rule.ByteSizeVariableName;
				this.customMessageTextBox.Text = rule.CustomMessage;
				this.endingConditions.AddAll(rule.EndingConditions);
				this.endingModeComboBox.SelectedItem = rule.EndingMode;
				this.endingPatternEditor.Pattern = rule.EndingPattern;
				this.endingPreActions.AddAll(rule.EndingPreActions);
				this.endingPostActions.AddAll(rule.EndingPostActions);
				this.endingVariables.AddAll(rule.EndingVariables);
				this.minDurationTextBox.Value = rule.MinDuration;
				this.maxDurationTextBox.Value = rule.MaxDuration;
				this.quantityVarNameTextBox.Text = rule.QuantityVariableName;
			}
			else
			{
				this.operationNameTextBox.Text = this.DefaultOperationName;
				this.resultTypeComboBox.SelectedItem = this.DefaultResultType;
				if (this.DefaultBeginningConditions != null)
					this.beginningConditions.AddAll(this.DefaultBeginningConditions);
				this.beginningPatternEditor.Pattern = this.DefaultBeginningPattern;
				if (this.DefaultBeginningPreActions != null)
					this.beginningPreActions.AddAll(this.DefaultBeginningPreActions);
				if (this.DefaultBeginningPostActions != null)
					this.beginningPostActions.AddAll(this.DefaultBeginningPostActions);
				this.byteSizeUnitComboBox.SelectedItem = default(IO.FileSizeUnit);
				if (this.DefaultEndingConditions != null)
					this.endingConditions.AddAll(this.DefaultEndingConditions);
				this.endingModeComboBox.SelectedItem = OperationEndingMode.FirstInFirstOut;
				this.endingPatternEditor.Pattern = this.DefaultEndingPattern;
				if (this.DefaultEndingPreActions != null)
					this.endingPreActions.AddAll(this.DefaultEndingPreActions);
				if (this.DefaultEndingPostActions != null)
					this.endingPostActions.AddAll(this.DefaultEndingPostActions);
			}
			this.SynchronizationContext.Post(() =>
			{
				if (!this.beginningPatternEditor.ShowTutorialIfNeeded(this.Get<TutorialPresenter>("tutorialPresenter"), this.operationNameTextBox))
					this.operationNameTextBox.Focus();
			});
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() 
			&& !string.IsNullOrWhiteSpace(this.operationNameTextBox.Text) 
			&& this.beginningPatternEditor.Pattern != null
			&& this.endingPatternEditor.Pattern != null
			&& this.minDurationTextBox.IsTextValid
			&& this.maxDurationTextBox.IsTextValid
			&& this.minDurationTextBox.Value.Let(minDuration =>
			{
				return this.maxDurationTextBox.Value.Let(maxDuration =>
				{
					if (!minDuration.HasValue || !maxDuration.HasValue)
						return true;
					return minDuration <= maxDuration;
				});
			});
		

		// Remove ending variable.
		void RemoveEndingVariable(ListBoxItem item)
		{
			var variable = (string)item.DataContext.AsNonNull();
			this.endingVariables.Remove(variable);
			this.endingVariableListBox.SelectedItem = null;
			this.endingVariableListBox.Focus();
		}


		/// <summary>
		/// Command to remove ending variable.
		/// </summary>
		public ICommand RemoveEndingVariableCommand { get; }
		

		/// <summary>
		/// Get or set rule to be edited.
		/// </summary>
		public OperationDurationAnalysisRuleSet.Rule? Rule { get; set; }
	}
}
