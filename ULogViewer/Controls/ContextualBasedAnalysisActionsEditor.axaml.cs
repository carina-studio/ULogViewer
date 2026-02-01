using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using ListBox = Avalonia.Controls.ListBox;
using Window = Avalonia.Controls.Window;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ContextualBasedAnalysisAction"/>s.
/// </summary>
class ContextualBasedAnalysisActionsEditor : UserControl<IULogViewerApplication>
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
	readonly ListBox actionListBox;
	readonly ToggleButton addActionButton;
	readonly ContextMenu addActionMenu;
	ScrollBarVisibility verticalScrollBarVisibility;
	Window? window;


	/// <summary>
	/// Initialize new <see cref="ContextualBasedAnalysisActionsEditor"/> instance.
	/// </summary>
	public ContextualBasedAnalysisActionsEditor()
	{
		this.EditActionCommand = new Command<ListBoxItem>(this.EditAction);
		this.Focusable = true;
		this.RemoveActionCommand = new Command<ListBoxItem>(this.RemoveAction);
		AvaloniaXamlLoader.Load(this);
		this.actionListBox = this.Get<AppSuite.Controls.ListBox>(nameof(actionListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (sender, e) => _ = this.EditAction((ContextualBasedAnalysisAction)e.Item);
			it.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty).Subscribe(visibility =>
			{
				this.SetAndRaise(VerticalScrollBarVisibilityProperty, ref this.verticalScrollBarVisibility, visibility);
			});
			ListBoxItemDragging.SetItemDraggingEnabled(it, true);
			it.AddHandler(ListBoxItemDragging.ItemDragStartedEvent, (_, e) =>
			{
				if (this.Actions.Count <= 1)
					e.Handled = true;
			});
			it.AddHandler(ListBoxItemDragging.ItemDroppedEvent, (_, e) =>
			{
				var actions = this.Actions;
				var actionCount = actions.Count;
				var startIndex = e.StartItemIndex;
				var index = e.ItemIndex;
				if (startIndex >= 0 && startIndex < actionCount && index >= 0 && index < actionCount && startIndex != index)
				{
					if (it.TryMoveItem<ContextualBasedAnalysisAction>(startIndex, index))
					{
						it.SelectedIndex = index;
						it.FocusSelectedItem();
					}
					else
						it.SelectedIndex = -1;
				}
			});
			it.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
			{
				if (!it.IsSelectedItemFocused)
					it.SelectedIndex = -1;
			});
		});
		this.addActionButton = this.Get<ToggleButton>(nameof(addActionButton));
		this.addActionMenu = ((ContextMenu)this.Resources[nameof(addActionMenu)].AsNonNull()).Also(it =>
		{
			it.Closed += (_, _) => 
				this.SynchronizationContext.Post(() => this.addActionButton.IsChecked = false);
			it.Opened += (_, _) => 
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
		this.AddHandler(KeyUpEvent, (_, e) => this.OnPreviewKeyUp(e), RoutingStrategies.Tunnel);
	}


	/// <summary>
	/// Get of set list of actions to be edited.
	/// </summary>
	public IList<ContextualBasedAnalysisAction> Actions
	{
		get => this.GetValue(ActionsProperty);
		set => this.SetValue(ActionsProperty, value);
	}


	// Add action.
	void AddAction(ContextualBasedAnalysisAction? action)
	{
		var actions = this.Actions;
		if (action is null)
			return;
		actions.Add(action);
		this.actionListBox.SelectedItem = action;
		this.actionListBox.FocusSelectedItem();
	}


	/// <summary>
	/// Add action.
	/// </summary>
	public async Task AddCopyVarAction()
	{
		if (this.window is not null)
			this.AddAction(await new CopyVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
	}


	/// <summary>
	/// Add action.
	/// </summary>
	public async Task AddDequeueToVarAction()
	{
		if (this.window is not null)
			this.AddAction(await new DequeueToVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
	}


	/// <summary>
	/// Add action.
	/// </summary>
	public async Task AddEnqueueVarAction()
	{
		if (this.window is not null)
			this.AddAction(await new EnqueueVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
	}


	/// <summary>
	/// Add action.
	/// </summary>
	public async Task AddPopToVarAction()
	{
		if (this.window is not null)
			this.AddAction(await new PopToVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
	}


	/// <summary>
	/// Add action.
	/// </summary>
	public async Task AddPushVarAction()
	{
		if (this.window is not null)
			this.AddAction(await new PushVarEditorDialog().ShowDialog<ContextualBasedAnalysisAction?>(this.window));
	}


	// Edit action.
	void EditAction(ListBoxItem item) =>
		_ = this.EditAction((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
	

	// Edit action.
	async Task EditAction(ContextualBasedAnalysisAction action)
	{
		// find action
		var actions = this.Actions;
		var index = actions.IndexOf(action);
		if (index < 0)
			return;
		
		// edit
		if (this.window is null)
			return;
		var newAction = action switch
		{
			CopyVariableAction cvAction => await new CopyVarEditorDialog { Action = cvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
			DequeueToVariableAction dtvAction => await new DequeueToVarEditorDialog { Action = dtvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
			EnqueueVariableAction evAction => await new EnqueueVarEditorDialog { Action = evAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
			PopToVariableAction ptvAction => await new PopToVarEditorDialog { Action = ptvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
			PushVariableAction pvAction => await new PushVarEditorDialog { Action = pvAction }.ShowDialog<ContextualBasedAnalysisAction?>(this.window),
			_ => throw new NotImplementedException(),
		};

		// update action
		if (newAction is not null && newAction != action)
		{
			actions[index] = newAction;
			this.actionListBox.SelectedItem = newAction;
		}
		else
			this.actionListBox.SelectedItem = action;
		this.actionListBox.FocusSelectedItem();
	}


	/// <summary>
	/// Command to edit action.
	/// </summary>
	public ICommand EditActionCommand { get; }
	

	/// <inheritdoc/>
	protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		base.OnAttachedToLogicalTree(e);
		this.window = this.FindLogicalAncestorOfType<Window>().AsNonNull();
	}


	/// <inheritdoc/>
	protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		this.window = null;
		base.OnDetachedFromLogicalTree(e);
	}
	
	
	// Called before passing key up event to children.
	void OnPreviewKeyUp(KeyEventArgs e)
	{
		if (!this.actionListBox.IsSelectedItemFocused)
			return;
		if (e.Key == Key.Enter && this.actionListBox.SelectedItem is ContextualBasedAnalysisAction action)
			_ = this.EditAction(action);
	}


	// Remove action.
	void RemoveAction(ListBoxItem item)
	{
		var actions = this.Actions;
		var index = actions.IndexOf((ContextualBasedAnalysisAction)item.DataContext.AsNonNull());
		if (index < 0)
			return;
		this.actionListBox.SelectedItem = null;
		actions.RemoveAt(index);
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