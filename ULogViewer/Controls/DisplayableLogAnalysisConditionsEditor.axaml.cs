using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="DisplayableLogAnalysisCondition"/>s.
	/// </summary>
	partial class DisplayableLogAnalysisConditionsEditor : CarinaStudio.Controls.UserControl<IULogViewerApplication>
	{
		/// <summary>
		/// Property of <see cref="Conditions"/>.
		/// </summary>
		public static readonly StyledProperty<IList<DisplayableLogAnalysisCondition>> ConditionsProperty = AvaloniaProperty.Register<DisplayableLogAnalysisConditionsEditor, IList<DisplayableLogAnalysisCondition>>(nameof(Conditions));
		/// <summary>
		/// Property of <see cref="VerticalScrollBarVisibility"/>.
		/// </summary>
		public static readonly DirectProperty<DisplayableLogAnalysisConditionsEditor, ScrollBarVisibility> VerticalScrollBarVisibilityProperty = AvaloniaProperty.RegisterDirect<DisplayableLogAnalysisConditionsEditor, ScrollBarVisibility>(nameof(VerticalScrollBarVisibility), 
			c => c.verticalScrollBarVisibility,
			(c, v) => ScrollViewer.SetVerticalScrollBarVisibility(c.conditionListBox, v));


		// Fields.
		readonly ToggleButton addConditionButton;
		readonly ContextMenu addConditionMenu;
		readonly Avalonia.Controls.ListBox conditionListBox;
		ScrollBarVisibility verticalScrollBarVisibility;
		Avalonia.Controls.Window? window;


		/// <summary>
		/// Initialize new <see cref="DisplayableLogAnalysisConditionsEditor"/> instance.
		/// </summary>
		public DisplayableLogAnalysisConditionsEditor()
		{
			this.EditConditionCommand = new Command<ListBoxItem>(this.EditCondition);
			this.RemoveConditionCommand = new Command<ListBoxItem>(this.RemoveCondition);
			AvaloniaXamlLoader.Load(this);
			this.addConditionButton = this.Get<ToggleButton>(nameof(addConditionButton));
			this.addConditionMenu = ((ContextMenu)this.Resources[nameof(addConditionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => 
					this.SynchronizationContext.Post(() => this.addConditionButton.IsChecked = false);
				it.MenuOpened += (_, e) => 
				{
					if (Platform.IsMacOS)
					{
						this.SynchronizationContext.PostDelayed(() =>
						{
							ToolTip.SetIsOpen(this.addConditionButton, true);
							ToolTip.SetIsOpen(this.addConditionButton, false);
						}, 100);
					}
					this.SynchronizationContext.Post(() => this.addConditionButton.IsChecked = true);
				};
			});
			this.conditionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(conditionListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditCondition((DisplayableLogAnalysisCondition)e.Item);
				it.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty).Subscribe(visibility =>
				{
					this.SetAndRaise<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty, ref this.verticalScrollBarVisibility, visibility);
				});
			});
		}


		// Add condition.
		void AddCondition(DisplayableLogAnalysisCondition? condition)
		{
			var conditions = this.Conditions;
			if (conditions == null || condition == null)
				return;
			conditions.Add(condition);
			this.conditionListBox.SelectedItem = condition;
			this.conditionListBox.Focus();
		}


		/// <summary>
		/// Add condition.
		/// </summary>
		public async void AddVarAndConstComparisonCondition()
		{
			if (this.window == null)
				return;
			this.AddCondition(await new VarAndConstComparisonEditorDialog().ShowDialog<DisplayableLogAnalysisCondition?>(this.window));
		}


		/// <summary>
		/// Add condition.
		/// </summary>
		public async void AddVarsComparisonCondition()
		{
			if (this.window == null)
				return;
			this.AddCondition(await new VarsComparisonEditorDialog().ShowDialog<DisplayableLogAnalysisCondition?>(this.window));
		}


		/// <summary>
		/// Get of set list of conditions to be edited.
		/// </summary>
		public IList<DisplayableLogAnalysisCondition> Conditions
		{
			get => this.GetValue<IList<DisplayableLogAnalysisCondition>>(ConditionsProperty);
			set => this.SetValue<IList<DisplayableLogAnalysisCondition>>(ConditionsProperty, value);
		}


		// Edit condition.
		void EditCondition(ListBoxItem item)
		{
			if (item.DataContext is DisplayableLogAnalysisCondition condition)
				this.EditCondition(condition);
		}


		// Edit condition.
		async void EditCondition(DisplayableLogAnalysisCondition condition)
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
				VariableAndConstantComparisonCondition vaccCondition => await new VarAndConstComparisonEditorDialog() { Condition = vaccCondition }.ShowDialog<DisplayableLogAnalysisCondition?>(this.window),
				VariablesComparisonCondition vcCondition => await new VarsComparisonEditorDialog() { Condition = vcCondition }.ShowDialog<DisplayableLogAnalysisCondition?>(this.window),
				_ => throw new NotImplementedException(),
			};
			if (newCondition is null || newCondition == condition)
				return;
			
			// update
			conditions[index] = newCondition;
			this.conditionListBox.SelectedItem = newCondition;
			this.conditionListBox.Focus();
		}


		/// <summary>
		/// Command to edit condition.
		/// </summary>
		public ICommand EditConditionCommand { get; }
		

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
			if (conditions == null || item.DataContext is not DisplayableLogAnalysisCondition condition)
				return;
			conditions.Remove(condition);
			this.conditionListBox.SelectedItem = null;
			this.conditionListBox.Focus();
		}


		/// <summary>
		/// Command to remove condition.
		/// </summary>
		public ICommand RemoveConditionCommand { get; }


		/// <summary>
		/// Show menu for adding condition.
		/// </summary>
		public void ShowAddConditionMenu() =>
			this.addConditionMenu.Open(this.addConditionButton);
		

		/// <summary>
		/// Get or set visibility of vertical scrollbar.
		/// </summary>
		public ScrollBarVisibility VerticalScrollBarVisibility
		{
			get => this.verticalScrollBarVisibility;
			set => ScrollViewer.SetVerticalScrollBarVisibility(this.conditionListBox, value);
		}
	}
}
