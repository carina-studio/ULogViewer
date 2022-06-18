using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="ContextualBasedAnalysisCondition"/>s.
	/// </summary>
	partial class ContextualBasedAnalysisConditionsEditor : CarinaStudio.Controls.UserControl<IULogViewerApplication>
	{
		/// <summary>
		/// Property of <see cref="Conditions"/>.
		/// </summary>
		public static readonly AvaloniaProperty<IList<ContextualBasedAnalysisCondition>> ConditionsProperty = AvaloniaProperty.Register<ContextualBasedAnalysisActionsEditor, IList<ContextualBasedAnalysisCondition>>(nameof(Conditions));


		// Fields.
		readonly ToggleButton addConditionButton;
		readonly ContextMenu addConditionMenu;
		readonly Avalonia.Controls.ListBox conditionListBox;
		Avalonia.Controls.Window? window;


		/// <summary>
		/// Initialize new <see cref="ContextualBasedAnalysisConditionsEditor"/> instance.
		/// </summary>
		public ContextualBasedAnalysisConditionsEditor()
		{
			AvaloniaXamlLoader.Load(this);
			this.addConditionButton = this.Get<ToggleButton>(nameof(addConditionButton));
			this.addConditionMenu = ((ContextMenu)this.Resources[nameof(addConditionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.addConditionButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.addConditionButton.IsChecked = true);
			});
			this.conditionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(conditionListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditCondition((ContextualBasedAnalysisCondition)e.Item);
			});
		}


		// Add condition.
		void AddCondition(ContextualBasedAnalysisCondition? condition)
		{
			var conditions = this.Conditions;
			if (conditions == null || condition == null)
				return;
			conditions.Add(condition);
			this.conditionListBox.SelectedItem = condition;
			this.conditionListBox.Focus();
		}


		// Add condition.
		async void AddVarAndConstComparisonCondition()
		{
			if (this.window == null)
				return;
			this.AddCondition(await new VarAndConstComparisonEditorDialog().ShowDialog<ContextualBasedAnalysisCondition?>(this.window));
		}


		// Add condition.
		async void AddVarsComparisonCondition()
		{
			if (this.window == null)
				return;
			this.AddCondition(await new VarsComparisonEditorDialog().ShowDialog<ContextualBasedAnalysisCondition?>(this.window));
		}


		/// <summary>
		/// Get of set list of conditions to be edited.
		/// </summary>
		public IList<ContextualBasedAnalysisCondition> Conditions
		{
			get => this.GetValue<IList<ContextualBasedAnalysisCondition>>(ConditionsProperty);
			set => this.SetValue<IList<ContextualBasedAnalysisCondition>>(ConditionsProperty, value);
		}


		// Edit condition.
		void EditCondition(ListBoxItem item)
		{
			if (item.DataContext is ContextualBasedAnalysisCondition condition)
				this.EditCondition(condition);
		}
		async void EditCondition(ContextualBasedAnalysisCondition condition)
		{
			// find position
			if (this.window == null)
				return;
			var conditions = this.Conditions;
			if (conditions == null)
				return;
			var index = conditions.IndexOf(condition);
			if (index < 0)
				return;
			
			// edit
			var newCondition = condition switch
			{
				VariableAndConstantComparisonCondition vaccCondition => await new VarAndConstComparisonEditorDialog() { Condition = vaccCondition }.ShowDialog<ContextualBasedAnalysisCondition?>(this.window),
				VariablesComparisonCondition vcCondition => await new VarsComparisonEditorDialog() { Condition = vcCondition }.ShowDialog<ContextualBasedAnalysisCondition?>(this.window),
				_ => throw new NotImplementedException(),
			};
			if (newCondition == null || newCondition == condition)
				return;
			
			// update
			conditions[index] = newCondition;
			this.conditionListBox.SelectedItem = newCondition;
			this.conditionListBox.Focus();
		}
		

		/// <inheritdoc/>
		protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			base.OnAttachedToLogicalTree(e);
			this.window = this.FindLogicalAncestorOfType<Avalonia.Controls.Window>().AsNonNull();
		}


		/// <inheritdoc/>
		protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			this.window = null;
			base.OnDetachedFromLogicalTree(e);
		}


		// Remove condition.
		void RemoveCondition(ListBoxItem item)
		{
			var conditions = this.Conditions;
			if (conditions == null || item.DataContext is not ContextualBasedAnalysisCondition condition)
				return;
			conditions.Remove(condition);
			this.conditionListBox.SelectedItem = null;
			this.conditionListBox.Focus();
		}


		// show menu for adding condition.
		void ShowAddConditionMenu() =>
			this.addConditionMenu.Open(this.addConditionButton);
	}
}
