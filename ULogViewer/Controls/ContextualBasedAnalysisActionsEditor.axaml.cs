using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="ContextualBasedAnalysisAction"/>s.
	/// </summary>
	partial class ContextualBasedAnalysisActionsEditor : CarinaStudio.Controls.UserControl<IULogViewerApplication>
	{
		/// <summary>
		/// Property of <see cref="Actions"/>.
		/// </summary>
		public static readonly AvaloniaProperty<IList<ContextualBasedAnalysisAction>> ActionsProperty = AvaloniaProperty.Register<ContextualBasedAnalysisActionsEditor, IList<ContextualBasedAnalysisAction>>(nameof(Actions));


		// Static fields.
		static readonly IObservable<object?> NullValueObservable = new MutableObservableValue<object?>();


		// Fields.
		readonly Avalonia.Controls.ListBox actionListBox;
		readonly ToggleButton addActionButton;
		readonly ContextMenu addActionMenu;
		Avalonia.Controls.Window? window;


		/// <summary>
		/// Initialize new <see cref="ContextualBasedAnalysisActionsEditor"/> instance.
		/// </summary>
		public ContextualBasedAnalysisActionsEditor()
		{
			AvaloniaXamlLoader.Load(this);
			this.actionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(actionListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditAction((ContextualBasedAnalysisAction)e.Item);
			});
			this.addActionButton = this.Get<ToggleButton>(nameof(addActionButton));
			this.addActionMenu = ((ContextMenu)this.Resources[nameof(addActionMenu)].AsNonNull()).Also(it =>
			{
				var nullValueToken = (IDisposable?)null;
				it.MenuClosed += (_, e) => 
				{
					Global.RunWithoutError(() => nullValueToken?.Dispose());
					nullValueToken = null;
					this.SynchronizationContext.Post(() => this.addActionButton.IsChecked = false);
				};
				it.MenuOpened += (_, e) => 
				{
					nullValueToken = this.addActionButton.Bind(ToolTip.TipProperty, NullValueObservable);
					this.SynchronizationContext.Post(() => this.addActionButton.IsChecked = true);
				};
			});
		}


		/// <summary>
		/// Get of set list of actions to be edited.
		/// </summary>
		public IList<ContextualBasedAnalysisAction> Actions
		{
			get => this.GetValue<IList<ContextualBasedAnalysisAction>>(ActionsProperty);
			set => this.SetValue<IList<ContextualBasedAnalysisAction>>(ActionsProperty, value);
		}


		// Add action.
		void AddAction(ContextualBasedAnalysisAction? action)
		{
			var actions = this.Actions;
			if (actions == null || action == null)
				return;
			actions.Add(action);
			this.actionListBox.SelectedItem = action;
			this.actionListBox.Focus();
		}


		// Add action.
		async void AddDequeueToVarAction()
		{
			if (this.window != null)
				this.AddAction(await new DequeueToVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		// Add action.
		async void AddEnqueueVarAction()
		{
			if (this.window != null)
				this.AddAction(await new EnqueueVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		// Add action.
		async void AddPopToVarAction()
		{
			if (this.window != null)
				this.AddAction(await new PopToVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		// Add action.
		async void AddPushVarAction()
		{
			if (this.window != null)
				this.AddAction(await new PushVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		// Edit action.
		void EditAction(ListBoxItem item) =>
			this.EditAction((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
		async void EditAction(ContextualBasedAnalysisAction action)
		{
			// find action
			var actions = this.Actions;
			if (actions == null)
				return;
			var index = actions.IndexOf(action);
			if (index < 0)
				return;
			
			// edit
			if (this.window == null)
				return;
			var newAction = action switch
			{
				DequeueToVariableAction dtvAction => await new DequeueToVarEditorDialog() { Action = dtvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
				EnqueueVariableAction evAction => await new EnqueueVarEditorDialog() { Action = evAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
				PopToVariableAction ptvAction => await new PopToVarEditorDialog() { Action = ptvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
				PushVariableAction pvAction => await new PushVarEditorDialog() { Action = pvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
				_ => throw new NotImplementedException(),
			};
			if (newAction == null || newAction == action)
				return;

			// update action
			actions[index] = newAction;
			this.actionListBox.SelectedItem = newAction;
			this.actionListBox.Focus();
		}


		// Move action down.
		void MoveActionDown(ListBoxItem item)
		{
			var actions = this.Actions;
			if (actions == null)
				return;
			var index = actions.IndexOf((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
			if (index < 0 || index >= actions.Count - 1)
				return;
			if (actions is ObservableList<ContextualBasedAnalysisAction> observableList)
				observableList.Move(index, index + 1);
			else
			{
				var action = actions[index];
				actions.RemoveAt(index);
				actions.Insert(index + 1, action);
			}
			this.actionListBox.SelectedIndex = index + 1;
			this.actionListBox.Focus();
		}


		// Move action up.
		void MoveActionUp(ListBoxItem item)
		{
			var actions = this.Actions;
			if (actions == null)
				return;
			var index = actions.IndexOf((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
			if (index <= 0)
				return;
			if (actions is ObservableList<ContextualBasedAnalysisAction> observableList)
				observableList.Move(index, index - 1);
			else
			{
				var action = actions[index];
				actions.RemoveAt(index);
				actions.Insert(index - 1, action);
			}
			this.actionListBox.SelectedIndex = index - 1;
			this.actionListBox.Focus();
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


		// Remove action.
		void RemoveAction(ListBoxItem item)
		{
			var actions = this.Actions;
			if (actions == null)
				return;
			var index = actions.IndexOf((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.actionListBox.SelectedItem = null;
			actions.RemoveAt(index);
			this.actionListBox.Focus();
		}


		// show menu for adding action.
		void ShowAddActionMenu() =>
			this.addActionMenu.Open(this.addActionButton);
	}
}
