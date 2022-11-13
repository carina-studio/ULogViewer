using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Windows.Input;

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
		public static readonly StyledProperty<IList<ContextualBasedAnalysisAction>> ActionsProperty = AvaloniaProperty.Register<ContextualBasedAnalysisActionsEditor, IList<ContextualBasedAnalysisAction>>(nameof(Actions));
		/// <summary>
		/// Property of <see cref="VerticalScrollBarVisibility"/>.
		/// </summary>
		public static readonly DirectProperty<ContextualBasedAnalysisActionsEditor, ScrollBarVisibility> VerticalScrollBarVisibilityProperty = AvaloniaProperty.RegisterDirect<ContextualBasedAnalysisActionsEditor, ScrollBarVisibility>(nameof(VerticalScrollBarVisibility), 
			c => c.verticalScrollBarVisibility, 
			(c, v) => ScrollViewer.SetVerticalScrollBarVisibility(c.actionListBox, v));


		// Fields.
		readonly Avalonia.Controls.ListBox actionListBox;
		readonly ToggleButton addActionButton;
		readonly ContextMenu addActionMenu;
		ScrollBarVisibility verticalScrollBarVisibility;
		Avalonia.Controls.Window? window;


		/// <summary>
		/// Initialize new <see cref="ContextualBasedAnalysisActionsEditor"/> instance.
		/// </summary>
		public ContextualBasedAnalysisActionsEditor()
		{
			this.EditActionCommand = new Command<ListBoxItem>(this.EditAction);
			this.MoveActionDownCommand = new Command<ListBoxItem>(this.MoveActionDown);
			this.MoveActionUpCommand = new Command<ListBoxItem>(this.MoveActionUp);
			this.RemoveActionCommand = new Command<ListBoxItem>(this.RemoveAction);
			AvaloniaXamlLoader.Load(this);
			this.actionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(actionListBox)).Also(it =>
			{
				it.DoubleClickOnItem += (_, e) => this.EditAction((ContextualBasedAnalysisAction)e.Item);
				it.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty).Subscribe(visibility =>
				{
					this.SetAndRaise<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty, ref this.verticalScrollBarVisibility, visibility);
				});
			});
			this.addActionButton = this.Get<ToggleButton>(nameof(addActionButton));
			this.addActionMenu = ((ContextMenu)this.Resources[nameof(addActionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => 
					this.SynchronizationContext.Post(() => this.addActionButton.IsChecked = false);
				it.MenuOpened += (_, e) => 
				{
					if (Platform.IsMacOS)
					{
						this.SynchronizationContext.PostDelayed(() =>
						{
							ToolTip.SetIsOpen(this.addActionButton, true);
							ToolTip.SetIsOpen(this.addActionButton, false);
						}, 100);
					}
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


		/// <summary>
		/// Add action.
		/// </summary>
		public async void AddCopyVarAction()
		{
			if (this.window != null)
				this.AddAction(await new CopyVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		/// <summary>
		/// Add action.
		/// </summary>
		public async void AddDequeueToVarAction()
		{
			if (this.window != null)
				this.AddAction(await new DequeueToVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		/// <summary>
		/// Add action.
		/// </summary>
		public async void AddEnqueueVarAction()
		{
			if (this.window != null)
				this.AddAction(await new EnqueueVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		/// <summary>
		/// Add action.
		/// </summary>
		public async void AddPopToVarAction()
		{
			if (this.window != null)
				this.AddAction(await new PopToVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		/// <summary>
		/// Add action.
		/// </summary>
		public async void AddPushVarAction()
		{
			if (this.window != null)
				this.AddAction(await new PushVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
		}


		// Edit action.
		void EditAction(ListBoxItem item) =>
			this.EditAction((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
		

		// Edit action.
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
				CopyVariableAction cvAction => await new CopyVarEditorDialog() { Action = cvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
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


		/// <summary>
		/// Command to edit action.
		/// </summary>
		public ICommand EditActionCommand { get; }


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


		/// <summary>
		/// Command to move action down.
		/// </summary>
		public ICommand MoveActionDownCommand { get; }


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


		/// <summary>
		/// Command to move action up.
		/// </summary>
		public ICommand MoveActionUpCommand { get; }
		

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


		/// <summary>
		/// Command to remove action.
		/// </summary>
		public ICommand RemoveActionCommand { get; }


		/// <summary>
		/// Show menu for adding action.
		/// </summary>
		public void ShowAddActionMenu() =>
			this.addActionMenu.Open(this.addActionButton);
		

		/// <summary>
		/// Get or set visibility of vertical scrollbar.
		/// </summary>
		public ScrollBarVisibility VerticalScrollBarVisibility
		{
			get => this.verticalScrollBarVisibility;
			set => ScrollViewer.SetVerticalScrollBarVisibility(this.actionListBox, value);
		}
	}
}
