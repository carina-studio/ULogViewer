using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Converters;
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
		/// <summary>
		/// Converter to convert from <see cref="ComparisonType"/> to string.
		/// </summary>
		public static readonly IValueConverter ComparisonTypeConverter = new EnumConverter(App.CurrentOrNull, typeof(ComparisonType));


		// Fields.
		readonly ToggleButton addBeginningConditionButton;
		readonly ToggleButton addEndingConditionButton;
		readonly ContextMenu addConditionMenu;
		readonly Avalonia.Controls.ListBox beginningConditionListBox;
		readonly ObservableList<ContextualBaseAnalysisCondition> beginningConditions = new();
		Regex? beginningPattern;
		readonly TextBox beginningPatternTextBox;
		readonly Avalonia.Controls.ListBox endingConditionListBox;
		readonly ObservableList<ContextualBaseAnalysisCondition> endingConditions = new();
		Regex? endingPattern;
		readonly TextBox endingPatternTextBox;
		readonly TextBox operationNameTextBox;


		/// <summary>
		/// Initialize new <see cref="OperationDurationAnalysisRuleEditorDialog"/> instance.
		/// </summary>
		public OperationDurationAnalysisRuleEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.addBeginningConditionButton = this.Get<ToggleButton>(nameof(addBeginningConditionButton)).Also(it =>
			{
				it.Click += (_, e) =>
				{
					if (it.IsChecked == true)
					{
						this.addConditionMenu!.Tag = it;
						this.addConditionMenu.Open(it);
					}
				};
			});
			this.addEndingConditionButton = this.Get<ToggleButton>(nameof(addEndingConditionButton)).Also(it =>
			{
				it.Click += (_, e) =>
				{
					if (it.IsChecked == true)
					{
						this.addConditionMenu!.Tag = it;
						this.addConditionMenu.Open(it);
					}
				};
			});
			this.addConditionMenu = ((ContextMenu)this.Resources[nameof(addConditionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) =>
				{
					this.SynchronizationContext.Post(() =>
					{
						(it.Tag as ToggleButton)?.Let(tb => tb.IsChecked = false);
						it.Tag = null;
					});
				};
				it.MenuOpened += (_, e) =>
				{
					this.SynchronizationContext.Post(() =>
						(it.Tag as ToggleButton)?.Let(tb => tb.IsChecked = true));
				};
			});
			this.beginningConditionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(beginningConditionListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditBeginningCondition((ContextualBaseAnalysisCondition)e.Item);
			});
			this.beginningPatternTextBox = this.Get<TextBox>(nameof(beginningPatternTextBox));
			this.endingConditionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(endingConditionListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditEndingCondition((ContextualBaseAnalysisCondition)e.Item);
			});
			this.endingPatternTextBox = this.Get<TextBox>(nameof(endingPatternTextBox));
			this.operationNameTextBox = this.Get<TextBox>(nameof(operationNameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
		}


		// Add condition.
		async void AddVarAndConstComparisonCondition()
		{
			var isBeginning = this.addConditionMenu.Tag == this.addBeginningConditionButton;
			var condition = await new VarAndConstComparisonEditorDialog().ShowDialog<VariableAndConstantComparisonCondition?>(this);
			if (condition == null)
				return;
			if (isBeginning)
			{
				this.beginningConditions.Add(condition);
				this.beginningConditionListBox.SelectedItem = condition;
				this.beginningConditionListBox.Focus();
			}
			else
			{
				this.endingConditions.Add(condition);
				this.endingConditionListBox.SelectedItem = condition;
				this.endingConditionListBox.Focus();
			}
		}
		async void AddVarsComparisonCondition()
		{
			var isBeginning = this.addConditionMenu.Tag == this.addBeginningConditionButton;
			var condition = await new VarsComparisonEditorDialog().ShowDialog<VariablesComparisonCondition?>(this);
			if (condition == null)
				return;
			if (isBeginning)
			{
				this.beginningConditions.Add(condition);
				this.beginningConditionListBox.SelectedItem = condition;
				this.beginningConditionListBox.Focus();
			}
			else
			{
				this.endingConditions.Add(condition);
				this.endingConditionListBox.SelectedItem = condition;
				this.endingConditionListBox.Focus();
			}
		}


		// Beginning conditions.
		IList<ContextualBaseAnalysisCondition> BeginningConditions { get => this.beginningConditions; }


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


		// Edit beginning condition.
		void EditBeginningCondition(ListBoxItem item)
		{
			if (item.DataContext is ContextualBaseAnalysisCondition condition)
				this.EditBeginningCondition(condition);
		}
		async void EditBeginningCondition(ContextualBaseAnalysisCondition condition)
		{
			// find position
			var index = this.beginningConditions.IndexOf(condition);
			if (index < 0)
				return;
			
			// edit
			var newCondition = await this.EditConditionAsync(condition);
			if (newCondition == null || newCondition == condition)
				return;
			
			// update
			this.beginningConditions[index] = newCondition;
			this.beginningConditionListBox.SelectedItem = newCondition;
			this.beginningConditionListBox.Focus();
		}


		// Edit condition.
		void EditCondition(ListBoxItem item)
		{
			if (item.FindAncestorOfType<Avalonia.Controls.ListBox>() == this.beginningConditionListBox)
				this.EditBeginningCondition(item);
			else
				this.EditEndingCondition(item);
		}


		// Edit condition.
		async Task<ContextualBaseAnalysisCondition?> EditConditionAsync(ContextualBaseAnalysisCondition condition) => condition switch
		{
			VariableAndConstantComparisonCondition varAndConstComparisonCondition => await new VarAndConstComparisonEditorDialog()
			{
				Condition = varAndConstComparisonCondition,
			}.ShowDialog<ContextualBaseAnalysisCondition?>(this),
			VariablesComparisonCondition varsComparisonCondition => await new VarsComparisonEditorDialog()
			{
				Condition = varsComparisonCondition,
			}.ShowDialog<ContextualBaseAnalysisCondition?>(this),
			_ => throw new NotImplementedException(),
		};


		// Edit ending condition.
		void EditEndingCondition(ListBoxItem item)
		{
			if (item.DataContext is ContextualBaseAnalysisCondition condition)
				this.EditEndingCondition(condition);
		}
		async void EditEndingCondition(ContextualBaseAnalysisCondition condition)
		{
			// find position
			var index = this.endingConditions.IndexOf(condition);
			if (index < 0)
				return;
			
			// edit
			var newCondition = await this.EditConditionAsync(condition);
			if (newCondition == null || newCondition == condition)
				return;
			
			// update
			this.endingConditions[index] = newCondition;
			this.endingConditionListBox.SelectedItem = newCondition;
			this.endingConditionListBox.Focus();
		}


		// Ending conditions.
		IList<ContextualBaseAnalysisCondition> EndingConditions { get => this.endingConditions; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var rule = this.Rule;
			var newRule = new OperationDurationAnalysisRuleSet.Rule(this.operationNameTextBox.Text.AsNonNull(),
				this.beginningPattern.AsNonNull(),
				this.beginningConditions,
				this.endingPattern.AsNonNull(),
				this.endingConditions);
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
				this.endingConditions.AddAll(rule.EndingConditions);
				this.endingPattern = rule.EndingPattern;
				this.endingPatternTextBox.Text = rule.EndingPattern.ToString();
			}
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
		/// Get or set rule to br edited.
		/// </summary>
		public OperationDurationAnalysisRuleSet.Rule? Rule { get; set; }


		// Remove beginning condition.
		void RemoveBeginningCondition(ListBoxItem item)
		{
			if (item.DataContext is not ContextualBaseAnalysisCondition condition)
				return;
			this.beginningConditions.Remove(condition);
			this.beginningConditionListBox.Focus();
		}


		// Remove condition.
		void RemoveCondition(ListBoxItem item)
		{
			if (item.FindAncestorOfType<Avalonia.Controls.ListBox>() == this.beginningConditionListBox)
				this.RemoveBeginningCondition(item);
			else
				this.RemoveEndingCondition(item);
		}


		// Remove ending condition.
		void RemoveEndingCondition(ListBoxItem item)
		{
			if (item.DataContext is not ContextualBaseAnalysisCondition condition)
				return;
			this.endingConditions.Remove(condition);
			this.endingConditionListBox.Focus();
		}
	}
}
