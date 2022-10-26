using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="OperationCountingAnalysisRuleSet.Rule"/>.
	/// </summary>
	partial class OperationCountingAnalysisRuleEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ObservableList<DisplayableLogAnalysisCondition> conditions = new();
		readonly TimeSpanTextBox intervalTextBox;
		readonly ComboBox levelComboBox;
		readonly TextBox operationNameTextBox;
		readonly PatternEditor patternEditor;
		readonly ComboBox resultTypeComboBox;


		/// <summary>
		/// Initialize new <see cref="OperationCountingAnalysisRuleEditorDialog"/> instance.
		/// </summary>
		public OperationCountingAnalysisRuleEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.intervalTextBox = this.Get<TimeSpanTextBox>(nameof(intervalTextBox)).Also(it =>
			{
				it.GetObservable(TimeSpanTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.levelComboBox = this.Get<ComboBox>(nameof(levelComboBox));
			this.operationNameTextBox = this.Get<TextBox>(nameof(operationNameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.patternEditor = this.Get<PatternEditor>(nameof(patternEditor)).Also(it =>
			{
				it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.resultTypeComboBox = this.Get<ComboBox>(nameof(resultTypeComboBox));
		}


		// Conditions.
		public IList<DisplayableLogAnalysisCondition> Conditions { get => this.conditions; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var rule = this.Rule;
			var newRule = new OperationCountingAnalysisRuleSet.Rule(
				this.operationNameTextBox.Text.AsNonNull(),
				this.intervalTextBox.Value.GetValueOrDefault(),
				this.patternEditor.Pattern.AsNonNull(),
				(Logs.LogLevel)this.levelComboBox.SelectedItem.AsNonNull(),
				this.conditions,
				(DisplayableLogAnalysisResultType)this.resultTypeComboBox.SelectedItem.AsNonNull()
			);
			if (newRule.Equals(rule))
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
				this.conditions.AddAll(rule.Conditions);
				this.intervalTextBox.Value = rule.Interval;
				this.levelComboBox.SelectedItem = rule.Level;
				this.operationNameTextBox.Text = rule.OperationName;
				this.patternEditor.Pattern = rule.Pattern;
				this.resultTypeComboBox.SelectedItem = rule.ResultType;
			}
			else
			{
				this.intervalTextBox.Value = TimeSpan.FromSeconds(1);
				this.levelComboBox.SelectedItem = Logs.LogLevel.Undefined;
				this.resultTypeComboBox.SelectedItem = DisplayableLogAnalysisResultType.Frequency;
			}
			this.SynchronizationContext.Post(() =>
			{
				if (!this.patternEditor.ShowTutorialIfNeeded(this.Get<TutorialPresenter>("tutorialPresenter"), this.operationNameTextBox))
					this.operationNameTextBox.Focus();
			});
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() 
			&& this.intervalTextBox.Value.GetValueOrDefault().Ticks > 0
			&& this.intervalTextBox.IsTextValid
			&& !string.IsNullOrWhiteSpace(this.operationNameTextBox.Text)
			&& this.patternEditor.Pattern != null;
		

		/// <summary>
		/// Get or set rule to be edited.
		/// </summary>
		public OperationCountingAnalysisRuleSet.Rule? Rule { get; set; }
	}
}
